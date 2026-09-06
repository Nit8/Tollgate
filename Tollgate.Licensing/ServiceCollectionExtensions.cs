using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tollgate.Licensing
{
    /// <summary>
    /// DI registration helpers for ASP.NET Core (and any DI-enabled app).
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register Tollgate with options bound from a configuration section.
        /// Usage:
        ///     builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
        /// </summary>
        public static IServiceCollection AddTollgate(
            this IServiceCollection services,
            IConfiguration configurationSection)
        {
            services.Configure<TollgateOptions>(configurationSection);
            return AddTollgateCore(services);
        }

        /// <summary>
        /// Register Tollgate with a delegate-based options builder.
        /// Usage:
        ///     builder.Services.AddTollgate(o => {
        ///         o.ServerUrl = "https://license.myapp.com";
        ///         o.AppId = "my-app";
        ///     });
        /// </summary>
        public static IServiceCollection AddTollgate(
            this IServiceCollection services,
            Action<TollgateOptions> configure)
        {
            services.Configure(configure);
            return AddTollgateCore(services);
        }

        private static IServiceCollection AddTollgateCore(IServiceCollection services)
        {
            // Register LicenseClient via IHttpClientFactory — proper HttpClient lifecycle management
            services.AddHttpClient<LicenseClient>();

            // Expose ILicenseClient as a singleton that wraps the LicenseClient.
            // Also wires up the static LicenseGate so non-DI code (attributes,
            // filters, LicenseGate.Current) can read the same instance.
            services.AddSingleton<Interfaces.ILicenseClient>(sp =>
            {
                var client = sp.GetRequiredService<LicenseClient>();
                LicenseGate.SetClient(client);
                return client;
            });

            return services;
        }
    }
}
