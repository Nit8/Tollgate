namespace Tollgate.Licensing
{
    /// <summary>
    /// All knobs you can set when configuring the Tollgate client.
    /// Bind from appsettings.json or set programmatically via
    /// <see cref="LicenseGate.Configure(Action{TollgateOptions})"/>.
    /// </summary>
    public sealed class TollgateOptions
    {
        /// <summary>Base URL of the Tollgate license server.</summary>
        public string ServerUrl { get; set; } = "http://localhost:7431";

        /// <summary>
        /// Your application ID (registered on the server). Multiple apps
        /// can share the same server, each scoped by AppId.
        /// </summary>
        public string AppId { get; set; } = "default";

        /// <summary>App version reported to the server (for analytics / min-version checks).</summary>
        public string AppVersion { get; set; } = "1.0.0";

        /// <summary>
        /// RSA public key (PEM or XML) used to verify JWT tokens issued by
        /// the server. The server keeps the matching private key.
        /// Optional — if empty, the client falls back to symmetric-secret verification.
        /// </summary>
        public string PublicKey { get; set; } = "";

        /// <summary>
        /// Shared secret used for symmetric HMAC JWT verification.
        /// Only used when <see cref="PublicKey"/> is empty AND the server
        /// is configured with symmetric signing. Avoid in production.
        /// </summary>
        public string SharedSecret { get; set; } = "";

        /// <summary>Cache file name (relative to LocalApplicationData/Tollgate/).</summary>
        public string CacheFile { get; set; } = "license.dat";

        /// <summary>Cache folder. Defaults to %LOCALAPPDATA%/Tollgate/&lt;AppId&gt;/ on Windows.</summary>
        public string? CacheDirectory { get; set; }

        /// <summary>HTTP timeout for license server calls.</summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Days a cached token is honored offline before re-validation is forced.
        /// Note: the actual hard limit is the JWT expiry set by the server
        /// (<c>Jwt:TokenLifetimeDays</c>, default 7 days). This option is a
        /// soft limit — when exceeded, the client will attempt online
        /// re-validation before falling back to free mode.
        /// </summary>
        public int OfflineGraceDays { get; set; } = 7;

        /// <summary>
        /// When false, the client throws if no license is configured.
        /// When true (default), the client runs in "free mode" — calls to
        /// EnsureFeature/EnsureTier still throw, but the app itself runs.
        /// </summary>
        public bool AllowFreeMode { get; set; } = true;
    }
}