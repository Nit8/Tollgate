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
        /// the server. The server keeps the matching private key
        /// (<c>Jwt:PrivateKey</c> in appsettings.json). This is the
        /// recommended production setup: no secret ever ships with the client.
        /// </summary>
        public string PublicKey { get; set; } = "";

        /// <summary>
        /// Shared secret used for symmetric HMAC JWT verification.
        /// Only used when <see cref="PublicKey"/> is empty AND the server
        /// is configured with symmetric signing (<c>Jwt:Secret</c>).
        /// Avoid in production — the secret can be extracted from the
        /// client binary and used to forge tokens.
        /// </summary>
        public string SharedSecret { get; set; } = "";

        /// <summary>
        /// Expected JWT issuer. Must match <c>Jwt:Issuer</c> on the server
        /// (default "TollgateServer").
        /// </summary>
        public string Issuer { get; set; } = "TollgateServer";

        /// <summary>
        /// Expected JWT audience. Must match <c>Jwt:Audience</c> on the server
        /// (default "TollgateClient").
        /// </summary>
        public string Audience { get; set; } = "TollgateClient";

        /// <summary>Cache file name (relative to LocalApplicationData/Tollgate/).</summary>
        public string CacheFile { get; set; } = "license.dat";

        /// <summary>Cache folder. Defaults to %LOCALAPPDATA%/Tollgate/&lt;AppId&gt;/ on Windows.</summary>
        public string? CacheDirectory { get; set; }

        /// <summary>HTTP timeout for license server calls.</summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Days a cached token is honored offline before online re-validation
        /// is forced. The token's own expiry (set by the server via
        /// <c>Jwt:TokenLifetimeDays</c>, default 7 days) is always enforced
        /// as the hard limit — the token is cryptographically verified, so it
        /// cannot be forged to extend the grace period.
        /// Set to 0 to force an online check on every startup.
        /// </summary>
        public int OfflineGraceDays { get; set; } = 7;

        /// <summary>
        /// When true (default), the app runs in "free mode" with no license —
        /// calls to LicenseGate.EnsureFeature / EnsureTier still throw
        /// <see cref="Tollgate.Abstractions.LicenseRequiredException"/>, but
        /// ungated code runs normally.
        /// When false, gate checks throw
        /// <see cref="Tollgate.Abstractions.LicenseNotConfiguredException"/>
        /// as soon as no valid license is active — use this for apps that must
        /// not run at all without a license.
        /// </summary>
        public bool AllowFreeMode { get; set; } = true;
    }
}
