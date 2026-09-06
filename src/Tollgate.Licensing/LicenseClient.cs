using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tollgate.Abstractions;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;
using Tollgate.Licensing.LicenseCache;
using Tollgate.Licensing.Interfaces;

namespace Tollgate.Licensing
{
    /// <summary>
    /// The main client. Handles online validation, JWT local caching,
    /// and offline grace-period enforcement. Use via <see cref="LicenseGate"/>
    /// static accessor, or inject as <c>ILicenseClient</c> in DI.
    /// </summary>
    public sealed class LicenseClient : ILicenseClient, IDisposable
    {
        private readonly HttpClient _http;
        private readonly TollgateOptions _options;
        private readonly LicenseStore _cache;
        private readonly ILogger<LicenseClient>? _log;
        private readonly bool _ownsHttp;
        private bool _disposed;

        public LicenseState Current { get; private set; } = LicenseState.Empty;

        /// <summary>The configured options (read-only view).</summary>
        public TollgateOptions Options => _options;

        public LicenseClient(HttpClient http, IOptions<TollgateOptions> options,
                             ILogger<LicenseClient>? log = null)
        {
            _options = options.Value;
            _http    = http;
            _log     = log;
            _ownsHttp = false;
            _cache   = new LicenseStore(_options);

            if (_http.BaseAddress is null)
                _http.BaseAddress = new Uri(_options.ServerUrl.TrimEnd('/'));

            _http.Timeout = _options.HttpTimeout;
        }

        // Convenience constructor for non-DI use (Console, WinForms, WPF).
        public LicenseClient(TollgateOptions options, ILogger<LicenseClient>? log = null)
            : this(new HttpClient(), Microsoft.Extensions.Options.Options.Create(options), log)
        {
            // Mark that we own this HttpClient so Dispose() cleans it up.
            _ownsHttp = true;
        }

        // ── Try to load saved license on startup ─────────────────
        public async Task<bool> TryLoadSavedLicenseAsync()
        {
            var cached = _cache.Load();
            if (cached is null) return false;

            // Validate the cached JWT locally (no server needed)
            if (TryValidateCachedToken(cached, out var tier))
            {
                Current = new LicenseState
                {
                    IsValid    = true,
                    Tier       = tier,
                    LicenseKey = cached.LicenseKey,
                    AppId      = cached.AppId,
                    MachineId  = MachineFingerprint.Get(),
                    ExpiresAt  = cached.ExpiresAt,
                    Features   = cached.Features,
                    CheckedAt  = DateTime.UtcNow,
                    Message    = "Loaded from local cache."
                };
                return true;
            }

            // Token expired — try online re-validation
            return (await ActivateKeyAsync(cached.LicenseKey)).IsValid;
        }

        // ── Activate a new key ───────────────────────────────────
        public async Task<ValidateLicenseResponse> ActivateKeyAsync(string licenseKey)
        {
            var key = licenseKey.Trim().ToUpperInvariant();
            try
            {
                var req = new ValidateLicenseRequest
                {
                    LicenseKey = key,
                    MachineId  = MachineFingerprint.Get(),
                    AppId      = _options.AppId,
                    AppVersion = _options.AppVersion
                };

                var res = await _http.PostAsJsonAsync("/api/license/validate", req);
                var body = await ParseAsync(res.Content);

                if (body?.IsValid == true)
                {
                    Current = new LicenseState
                    {
                        IsValid    = true,
                        Tier       = body.Tier,
                        LicenseKey = key,
                        AppId      = body.AppId,
                        MachineId  = req.MachineId,
                        ExpiresAt  = body.ExpiresAt,
                        Features   = body.Features,
                        CheckedAt  = DateTime.UtcNow,
                        Message    = body.Message
                    };

                    _cache.Save(new CachedLicense
                    {
                        LicenseKey = key,
                        Token      = body.Token,
                        AppId      = body.AppId,
                        ExpiresAt  = body.ExpiresAt,
                        Features   = body.Features,
                        Tier       = body.Tier,
                        CachedAt   = DateTime.UtcNow,
                    });

                    _log?.LogInformation("License activated: app={App} tier={Tier} features={Feats}",
                        body.AppId, body.Tier, string.Join(",", body.Features));
                }
                else
                {
                    _log?.LogWarning("License activation failed: app={App} key={Key} msg={Msg}",
                        _options.AppId, key, body?.Message ?? "(empty)");
                }
                return body ?? new ValidateLicenseResponse
                {
                    IsValid = false,
                    Message = "Empty server response."
                };
            }
            catch (Exception ex)
            {
                return new ValidateLicenseResponse
                {
                    IsValid = false,
                    Message = $"Cannot reach license server: {ex.Message}"
                };
            }
        }

        // ── Local JWT validation (offline support) ────────────────
        private bool TryValidateCachedToken(CachedLicense cached, out LicenseTier tier)
        {
            tier = LicenseTier.None;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt     = handler.ReadJwtToken(cached.Token);

                if (jwt.ValidTo < DateTime.UtcNow) return false;

                // Machine binding
                var mid = jwt.Claims.FirstOrDefault(c => c.Type == "mid")?.Value ?? "";
                if (mid != MachineFingerprint.Get()) return false;

                // App binding
                var app = jwt.Claims.FirstOrDefault(c => c.Type == "app")?.Value ?? "";
                if (!string.IsNullOrEmpty(app) && app != _options.AppId) return false;

                var tierStr = jwt.Claims.FirstOrDefault(c => c.Type == "tier")?.Value ?? "None";
                Enum.TryParse(tierStr, out tier);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────
        private static async Task<ValidateLicenseResponse?> ParseAsync(HttpContent content)
        {
            var raw = await content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Deserialize<ValidateLicenseResponse>(raw, options);
        }

        public void ClearLicense()
        {
            Current = LicenseState.Empty;
            _cache.Clear();
        }

        // ── IDisposable ────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Only dispose the HttpClient if we created it ourselves.
            // When injected via IHttpClientFactory, the factory owns it.
            if (_ownsHttp) _http.Dispose();
        }
    }
}