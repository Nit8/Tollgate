
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Tollgate.Server.Services;

namespace Tollgate.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // ─────────────────────────────────────────────────────────────
            //  Tollgate Server  —  self-hostable license & keygen API
            //
            //  Default URL:  http://0.0.0.0:5000
            //  Swagger UI:   http://localhost:5000/swagger
            //  SQLite file:  licenses.db (auto-created next to the binary)
            // ─────────────────────────────────────────────────────────────

            var builder = WebApplication.CreateBuilder(args);

            // ── Services ──────────────────────────────────────────────────
            builder.Services.AddControllers()
                .AddJsonOptions(opt =>
                {
                    // Serialize ALL enums as their string names, not integers.
                    // So Tier comes out as "Basic" / "Pro" instead of 2 / 3.
                    opt.JsonSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Tollgate Server", Version = "v1" });
                c.AddSecurityDefinition("AdminKey", new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    Name = "X-Admin-Key",
                    Description = "Admin password from appsettings.json → Admin:Key"
                });
                c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("AdminKey", document, null)] = []
                });
            });

            // SQLite — single file, zero config, perfect for self-hosted server.
            // For production with high load, switch to PostgreSQL or SQL Server
            // (just change the connection string and add the EF provider package).
            var dbPath = Path.Combine(AppContext.BaseDirectory, "licenses.db");
            builder.Services.AddDbContext<LicenseDbContext>(opt =>
                opt.UseSqlite($"Data Source={dbPath}"));

            builder.Services.AddScoped<TokenService>();
            builder.Services.AddMemoryCache();

            // CORS — restrict to your app's domain in production.
            builder.Services.AddCors(opt =>
                opt.AddDefaultPolicy(p =>
                    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            // Optional: rate limiting (basic in-memory)
            builder.Services.AddRateLimiter(o =>
            {
                o.AddFixedWindowLimiter("api", opt =>
                {
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.PermitLimit = 60;
                });
            });

            var app = builder.Build();

            // ── Auto-migrate DB on startup ────────────────────────────────
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
                db.Database.EnsureCreated();
            }

            // ── Middleware ────────────────────────────────────────────────
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();

            // Landing page
            app.MapGet("/", () => Results.Json(new
            {
                service = "Tollgate Server",
                version = "1.0.0",
                status = "running",
                docs = "/swagger",
                license = "/api/license/health"
            }));

            app.Run();
        }
    }
}
