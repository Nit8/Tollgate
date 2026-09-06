using System.Reflection;
using Tollgate.Abstractions;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Licensing
{
    // ─────────────────────────────────────────────────────────────
    //  LICENSE GATE — the simplest possible API
    //
    //  For Console / WinForms / WPF apps that don't use DI:
    //
    //      LicenseGate.Configure(o => { o.ServerUrl = "..."; o.AppId = "..."; });
    //      await LicenseGate.InitializeAsync();
    //      LicenseGate.EnsureFeature("export-pdf");  // throws if no license
    //
    //  For ASP.NET Core apps, register via:
    //      services.AddTollgate(...);
    //  and use [RequireFeature] / [RequireTier] on controllers/actions.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Static accessor for the active license. Set via
    /// <see cref="Configure(Action{TollgateOptions})"/> or by setting
    /// <see cref="Current"/> directly (for ASP.NET Core DI scenarios).
    /// </summary>
    public static class LicenseGate
    {
        private static LicenseClient? _client;
        private static TollgateOptions _options = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Configure the gate programmatically. Call this once at startup,
        /// before <see cref="InitializeAsync"/>.
        /// </summary>
        public static void Configure(Action<TollgateOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            lock (_lock)
            {
                configure(_options);
                // Re-create the client if already initialized
                _client?.Dispose();
                _client = new LicenseClient(_options);
            }
        }

        /// <summary>Configure from an explicit options object.</summary>
        public static void Configure(TollgateOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            lock (_lock)
            {
                _options = options;
                _client?.Dispose();
                _client = new LicenseClient(_options);
            }
        }

        /// <summary>
        /// Auto-discover tollgate.json from the standard search paths
        /// (env var TOLLGATE_CONFIG, app dir, CWD, user config dir) and
        /// configure from it. Throws <see cref="LicenseNotConfiguredException"/>
        /// if no config file is found.
        ///
        /// Call this at startup instead of <see cref="Configure(Action{TollgateOptions})"/>
        /// when you want config-driven setup with zero boilerplate.
        /// </summary>
        /// <returns>The path of the config file that was loaded (for logging).</returns>
        public static string ConfigureFromConfigFile()
        {
            foreach (var path in TollgateConfig.GetSearchPaths())
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var cfg = TollgateConfig.Load(path);
                    if (cfg is null) continue;
                    Configure(cfg.ToOptions());
                    _loadedConfigPath = path;
                    return path;
                }
                catch
                {
                    // Malformed file — try next location
                }
            }
            throw new LicenseNotConfiguredException(
                "No tollgate.json found. Searched:\n  - " +
                string.Join("\n  - ", TollgateConfig.GetSearchPaths()) +
                "\nCreate one with `tollgate-keygen init` or copy the template from " +
                "the Tollgate.Licensing package's build/ folder.");
        }

        /// <summary>
        /// Try to auto-discover and load a tollgate.json. Returns false if
        /// none found (instead of throwing). Useful for apps that want to
        /// fall back to programmatic configuration when no file exists.
        /// </summary>
        public static bool TryConfigureFromConfigFile()
        {
            try
            {
                ConfigureFromConfigFile();
                return true;
            }
            catch (LicenseNotConfiguredException)
            {
                return false;
            }
        }

        /// <summary>
        /// The path of the tollgate.json that was loaded (or null if
        /// configured programmatically). Useful for diagnostics: show the
        /// user "Loaded license config from C:\Users\me\...tollgate.json".
        /// </summary>
        public static string? LoadedConfigPath => _loadedConfigPath;
        private static string? _loadedConfigPath;

        /// <summary>Allow DI containers to inject the client after creation.</summary>
        public static void SetClient(LicenseClient client)
        {
            lock (_lock)
            {
                _client?.Dispose();
                _client = client;
                _options = client.Options;
            }
        }

        /// <summary>
        /// The current license state. Never null (returns
        /// <see cref="LicenseState.Empty"/> when no license is active).
        /// </summary>
        public static LicenseState Current => _client?.Current ?? LicenseState.Empty;

        /// <summary>The configured options (read-only view).</summary>
        public static TollgateOptions Options => _options;

        /// <summary>
        /// Try to load a previously-activated license from disk cache.
        /// Returns true if a valid (cached or re-validated) license was found.
        /// </summary>
        public static async Task<bool> TryLoadSavedLicenseAsync()
        {
            EnsureClient();
            return await _client!.TryLoadSavedLicenseAsync();
        }

        /// <summary>
        /// Alias for <see cref="TryLoadSavedLicenseAsync"/>. Convenience
        /// for the common startup pattern.
        /// </summary>
        public static Task<bool> InitializeAsync() => TryLoadSavedLicenseAsync();

        /// <summary>
        /// Activate a license key. On success, <see cref="Current"/> is updated.
        /// </summary>
        public static async Task<ValidateLicenseResponse> ActivateKeyAsync(string licenseKey)
        {
            EnsureClient();
            return await _client!.ActivateKeyAsync(licenseKey);
        }

        /// <summary>Remove the cached license and reset state.</summary>
        public static void ClearLicense()
        {
            _client?.ClearLicense();
        }

        // ── FEATURE / TIER CHECKS ────────────────────────────────

        /// <summary>True if the current license has the named feature.</summary>
        public static bool CanAccess(string feature) => Current.HasFeature(feature);

        /// <summary>True if the current license's tier meets or exceeds the requirement.</summary>
        public static bool CanAccess(LicenseTier required) => Current.MeetsTier(required);

        /// <summary>
        /// Throw <see cref="LicenseRequiredException"/> if the current license
        /// lacks the named feature.
        /// </summary>
        public static void EnsureFeature(string feature)
        {
            if (!Current.HasFeature(feature))
                throw new LicenseRequiredException(feature, Current.Tier);
        }

        /// <summary>
        /// Throw <see cref="LicenseRequiredException"/> if the current license's
        /// tier is below the required one.
        /// </summary>
        public static void EnsureTier(LicenseTier tier)
        {
            if (!Current.MeetsTier(tier))
                throw new LicenseRequiredException(tier, Current.Tier);
        }

        /// <summary>
        /// Reflect on the given method (or type) and enforce every
        /// <see cref="RequireFeatureAttribute"/> and <see cref="RequireTierAttribute"/>
        /// on it. Throw <see cref="LicenseRequiredException"/> on first failure.
        /// </summary>
        public static void EnsureAccessFor(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);

            foreach (var attr in method.GetCustomAttributes<RequireTierAttribute>())
                if (!Current.MeetsTier(attr.Tier))
                    throw new LicenseRequiredException(attr.Tier, Current.Tier);

            foreach (var attr in method.GetCustomAttributes<RequireFeatureAttribute>())
                if (!Current.HasFeature(attr.Feature))
                    throw new LicenseRequiredException(attr.Feature, Current.Tier);
        }

        /// <summary>
        /// Reflect on the given type and enforce attributes declared on it.
        /// </summary>
        public static void EnsureAccessFor(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            foreach (var attr in type.GetCustomAttributes<RequireTierAttribute>())
                if (!Current.MeetsTier(attr.Tier))
                    throw new LicenseRequiredException(attr.Tier, Current.Tier);

            foreach (var attr in type.GetCustomAttributes<RequireFeatureAttribute>())
                if (!Current.HasFeature(attr.Feature))
                    throw new LicenseRequiredException(attr.Feature, Current.Tier);
        }

        /// <summary>
        /// Reflect on the calling method (via stack walk) and enforce attributes.
        /// Use sparingly — reflection-based stack walking is slow.
        /// </summary>
        public static void EnsureAccessForCaller()
        {
            var frame = new System.Diagnostics.StackFrame(1);
            var method = frame.GetMethod();
            if (method is MethodInfo mi) EnsureAccessFor(mi);
        }

        // ── Internals ─────────────────────────────────────────────
        private static void EnsureClient()
        {
            if (_client is null)
            {
                lock (_lock)
                {
                    if (_client is null)
                        _client = new LicenseClient(_options);
                }
            }
        }
    }
}