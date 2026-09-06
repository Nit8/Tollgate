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
        // volatile: readers of Current/Options must never observe a
        // partially-constructed client while Configure/SetClient replaces it.
        private static volatile LicenseClient? _client;
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
                var old = _client;
                _client = new LicenseClient(_options);
                old?.Dispose();
            }
        }

        /// <summary>Configure from an explicit options object.</summary>
        public static void Configure(TollgateOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            lock (_lock)
            {
                _options = options;
                var old = _client;
                _client = new LicenseClient(_options);
                old?.Dispose();
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
            ArgumentNullException.ThrowIfNull(client);
            lock (_lock)
            {
                var old = _client;
                _client = client;
                _options = client.Options;
                // The DI container owns the new client's lifecycle; disposing
                // it here would yank it out from under the container.
                if (old is not null && !ReferenceEquals(old, client)) old.Dispose();
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
        /// Try to load a previously-activated license from the encrypted disk
        /// cache. The cached token is cryptographically verified (see
        /// <see cref="LicenseClient"/>). Returns true if a valid (cached or
        /// re-validated) license was found.
        /// </summary>
        public static Task<bool> TryLoadSavedLicenseAsync(CancellationToken cancellationToken = default)
        {
            EnsureClient();
            return _client!.TryLoadSavedLicenseAsync(cancellationToken);
        }

        /// <summary>
        /// Alias for <see cref="TryLoadSavedLicenseAsync(CancellationToken)"/>. Convenience
        /// for the common startup pattern.
        /// </summary>
        public static Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
            => TryLoadSavedLicenseAsync(cancellationToken);

        /// <summary>
        /// Activate a license key. On success, <see cref="Current"/> is updated.
        /// </summary>
        public static Task<ValidateLicenseResponse> ActivateKeyAsync(
            string licenseKey, CancellationToken cancellationToken = default)
        {
            EnsureClient();
            return _client!.ActivateKeyAsync(licenseKey, cancellationToken);
        }

        /// <summary>
        /// Release the machine binding of the current license on the server
        /// (license transfer to another machine) and clear the local cache.
        /// </summary>
        public static Task<ValidateLicenseResponse> DeactivateAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureClient();
            return _client!.DeactivateAsync(cancellationToken);
        }

        /// <summary>Remove the cached license and reset state (local only).</summary>
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
        /// lacks the named feature. Throws <see cref="LicenseNotConfiguredException"/>
        /// instead when no license is active and <see cref="TollgateOptions.AllowFreeMode"/>
        /// is false.
        /// </summary>
        public static void EnsureFeature(string feature)
        {
            EnsureConfigured();
            if (!Current.HasFeature(feature))
                throw new LicenseRequiredException(feature, Current.Tier);
        }

        /// <summary>
        /// Throw <see cref="LicenseRequiredException"/> if the current license's
        /// tier is below the required one. Throws <see cref="LicenseNotConfiguredException"/>
        /// instead when no license is active and <see cref="TollgateOptions.AllowFreeMode"/>
        /// is false.
        /// </summary>
        public static void EnsureTier(LicenseTier tier)
        {
            EnsureConfigured();
            if (!Current.MeetsTier(tier))
                throw new LicenseRequiredException(tier, Current.Tier);
        }

        /// <summary>
        /// Throw <see cref="LicenseRequiredException"/> unless the current
        /// license is a valid trial (a valid key with tier None).
        /// </summary>
        public static void EnsureTrial()
        {
            EnsureConfigured();
            if (!Current.IsTrial)
                throw new LicenseRequiredException(
                    "This action is only available during the trial period.");
        }

        /// <summary>
        /// Reflect on the given method (or type) and enforce every
        /// <see cref="RequireFeatureAttribute"/>, <see cref="RequireTierAttribute"/>
        /// and <see cref="RequireTrialAttribute"/> on it. Throw
        /// <see cref="LicenseRequiredException"/> on first failure.
        /// </summary>
        public static void EnsureAccessFor(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);
            EnsureConfigured();

            foreach (var attr in method.GetCustomAttributes<RequireTierAttribute>())
                if (!Current.MeetsTier(attr.Tier))
                    throw new LicenseRequiredException(attr.Tier, Current.Tier);

            foreach (var attr in method.GetCustomAttributes<RequireFeatureAttribute>())
                if (!Current.HasFeature(attr.Feature))
                    throw new LicenseRequiredException(attr.Feature, Current.Tier);

            foreach (var attr in method.GetCustomAttributes<RequireTrialAttribute>())
                if (!Current.IsTrial)
                    throw new LicenseRequiredException(
                        attr.DeniedMessage ?? "This action is only available during the trial period.");
        }

        /// <summary>
        /// Reflect on the given type and enforce attributes declared on it.
        /// </summary>
        public static void EnsureAccessFor(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            EnsureConfigured();

            foreach (var attr in type.GetCustomAttributes<RequireTierAttribute>())
                if (!Current.MeetsTier(attr.Tier))
                    throw new LicenseRequiredException(attr.Tier, Current.Tier);

            foreach (var attr in type.GetCustomAttributes<RequireFeatureAttribute>())
                if (!Current.HasFeature(attr.Feature))
                    throw new LicenseRequiredException(attr.Feature, Current.Tier);

            foreach (var attr in type.GetCustomAttributes<RequireTrialAttribute>())
                if (!Current.IsTrial)
                    throw new LicenseRequiredException(
                        attr.DeniedMessage ?? "This action is only available during the trial period.");
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

        /// <summary>
        /// Strict-mode enforcement: when no license is active and
        /// AllowFreeMode is false, the app must not run — fail hard with
        /// <see cref="LicenseNotConfiguredException"/> instead of a feature
        /// denial that looks like an upsell prompt.
        /// </summary>
        private static void EnsureConfigured()
        {
            if (!Current.IsValid && !_options.AllowFreeMode)
                throw new LicenseNotConfiguredException(
                    "No license is active and AllowFreeMode is disabled. " +
                    "Activate a license before using gated features.");
        }

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
