using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// <code>
        /// builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
        /// </code>
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
        /// <code>
        /// builder.Services.AddTollgate(o =>
        /// {
        ///     o.ServerUrl = "https://license.myapp.com";
        ///     o.AppId     = "my-app";
        ///     o.PublicKey = "-----BEGIN PUBLIC KEY-----...";
        /// });
        /// </code>
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
            // Enables IHttpClientFactory and registers the named client
            // configuration point ("Tollgate"). Handlers rotate correctly.
            services.AddHttpClient(LicenseClient.HttpClientName);

            // LicenseClient is a safe singleton: it holds no HttpClient of its
            // own — it rents a fresh one from the factory per operation, so
            // there is no captive dependency and DNS changes keep working.
            services.AddSingleton<LicenseClient>(sp => new LicenseClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IOptions<TollgateOptions>>(),
                sp.GetService<ILogger<LicenseClient>>()));

            // Expose ILicenseClient as the same singleton instance and wire
            // the static LicenseGate so non-DI code (attributes, filters,
            // LicenseGate.Current) sees the same license state.
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
