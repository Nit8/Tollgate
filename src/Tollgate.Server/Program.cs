
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Tollgate.Server.Data;
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
            //  Default URL:  http://0.0.0.0:7431
            //  Swagger UI:   http://localhost:7431/swagger (Development only)
            //  SQLite file:  configurable via Data:Path / Data__Path
            //                (defaults to /data/licenses.db in containers,
            //                 else next to the binary)
            // ─────────────────────────────────────────────────────────────

            var builder = WebApplication.CreateBuilder(args);

            // ── Fail fast on placeholder secrets in Production ──────────
            // A silent misconfiguration (the committed CHANGE_ME values)
            // must become an obvious startup error, not a public exploit.
            if (builder.Environment.IsProduction())
            {
                EnsureProductionSecrets(builder.Configuration);
            }

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
            //
            // Path resolution order:
            //   1. Data:Path / Data__Path configuration value (absolute, or
            //      relative to the app directory)
            //   2. /data/licenses.db when a /data directory exists (the Docker
            //      volume and the systemd ReadWritePaths convention)
            //   3. <appdir>/licenses.db (source-run / bare binary)
            var dbPath = ResolveDatabasePath(builder.Configuration);
            builder.Services.AddDbContext<LicenseDbContext>(opt =>
                opt.UseSqlite($"Data Source={dbPath}"));

            builder.Services.AddScoped<TokenService>();
            builder.Services.AddMemoryCache();

            // CORS — explicit origin allow-list in Production (comma-separated
            // "Cors:AllowedOrigins"); permissive in Development only.
            var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (builder.Environment.IsProduction() && allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    "CORS is locked down in Production: set Cors:AllowedOrigins (comma-separated) " +
                    "in appsettings.Production.json or the Cors__AllowedOrigins__0 env var. " +
                    "Example: Cors__AllowedOrigins__0=https://app.mycompany.com");
            }
            builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
            {
                if (allowedOrigins.Length > 0)
                    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                else
                    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); // Development
            }));

            // Rate limiting — applied to every controller action via
            // [EnableRateLimiting("api")] (see both controllers).
            builder.Services.AddRateLimiter(o =>
            {
                o.AddFixedWindowLimiter("api", opt =>
                {
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.PermitLimit = 60;
                    opt.QueueLimit = 0;
                });
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            });

            // Behind a reverse proxy (nginx), Kestrel must see the real
            // client IP for rate limiting and log forensics.
            if (!builder.Environment.IsDevelopment())
            {
                builder.Services.Configure<ForwardedHeadersOptions>(o =>
                {
                    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    // Trust the local proxy by default — tighten for multi-tier setups.
                    o.KnownIPNetworks.Clear();
                    o.KnownProxies.Clear();
                });
            }

            var app = builder.Build();

            // ── Auto-create DB schema on startup ─────────────────────────
            // (EF Core migrations are on the v1.1 roadmap; EnsureCreated is
            // safe for first deployment — it creates the schema if missing.)
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

            if (!app.Environment.IsDevelopment())
            {
                app.UseForwardedHeaders();
            }
            app.UseCors();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();

            // Landing page — version from assembly metadata, not a literal.
            var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            app.MapGet("/", () => Results.Json(new
            {
                service = "Tollgate Server",
                version = version,
                status = "running",
                docs = "/swagger",
                license = "/api/license/health"
            }));

            app.Run();
        }

        /// <summary>
        /// Resolve the SQLite file location (see comment at the AddDbContext
        /// call site for the full precedence order).
        /// </summary>
        private static string ResolveDatabasePath(IConfiguration cfg)
        {
            var configured = cfg["Data:Path"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(AppContext.BaseDirectory, configured);
            }

            // Docker volume / systemd convention: /data is writable.
            if (Directory.Exists("/data"))
                return Path.Combine("/data", "licenses.db");

            return Path.Combine(AppContext.BaseDirectory, "licenses.db");
        }

        /// <summary>
        /// Production guard: refuse to start with placeholder or weak
        /// secrets so a committed CHANGE_ME value can never reach a
        /// public deployment.
        /// </summary>
        private static void EnsureProductionSecrets(IConfiguration cfg)
        {
            var jwtSecret = cfg["Jwt:Secret"] ?? "";
            var jwtPrivateKey = cfg["Jwt:PrivateKey"] ?? "";
            var adminKey = cfg["Admin:Key"] ?? "";

            var placeholders = new[] { "REPLACE_WITH", "CHANGE_ME", "CHANGE_THIS", "change-me" };

            if (IsInvalid(adminKey, placeholders, minLength: 16))
                throw new InvalidOperationException(
                    "Admin:Key is missing, too short (<16 chars), or still a placeholder. " +
                    "Set a strong value via appsettings.Production.json or the Admin__Key env var.");

            // HMAC mode requires a >=32 char secret; RSA mode (PrivateKey) does not.
            var usingRsa = !string.IsNullOrWhiteSpace(jwtPrivateKey);
            if (!usingRsa && (IsInvalid(jwtSecret, placeholders, minLength: 32)))
                throw new InvalidOperationException(
                    "Jwt:Secret is missing, too short (<32 chars), or still a placeholder. " +
                    "Generate one with `openssl rand -base64 48`, or configure Jwt:PrivateKey " +
                    "(RSA PEM) to switch to asymmetric signing.");
        }

        private static bool IsInvalid(string value, string[] placeholders, int minLength) =>
            string.IsNullOrWhiteSpace(value) ||
            value.Length < minLength ||
            placeholders.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}
