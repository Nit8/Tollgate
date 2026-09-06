using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tollgate.Abstractions;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;
using Tollgate.Licensing.Interfaces;
using Tollgate.Licensing.LicenseCache;

namespace Tollgate.Licensing
{
    /// <summary>
    /// The main client. Handles online validation, cryptographically verified
    /// JWT local caching, and offline grace-period enforcement. Use via
    /// <see cref="LicenseGate"/> static accessor, or inject as
    /// <c>ILicenseClient</c> in DI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tokens are verified with the same rigor the server applies: signature
    /// (RSA public key or shared secret, resolved from
    /// <see cref="TollgateOptions"/>), issuer, audience, lifetime, machine
    /// binding and app binding. A cached token that fails any check is never
    /// honored — the client fails closed.
    /// </para>
    /// <para>
    /// For offline validation to work at all, configure either
    /// <see cref="TollgateOptions.PublicKey"/> (recommended — the server keeps
    /// the private key) or <see cref="TollgateOptions.SharedSecret"/>. Without
    /// a key the client always re-validates online and treats cached tokens as
    /// unverifiable.
    /// </para>
    /// </remarks>
    public sealed class LicenseClient : ILicenseClient, IDisposable
    {
        /// <summary>Name of the named <see cref="HttpClient"/> registered by AddTollgate.</summary>
        public const string HttpClientName = "Tollgate";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Exactly one of these is non-null:
        //   _http        → owned (created by the convenience ctor) or injected
        //                  (advanced/tests) — lives as long as this instance.
        //   _httpFactory → DI mode: a fresh HttpClient is rented from the
        //                  factory per operation, so DNS changes and handler
        //                  rotation keep working (no captive dependency).
        private readonly HttpClient? _http;
        private readonly IHttpClientFactory? _httpFactory;

        private readonly TollgateOptions _options;
        private readonly LicenseStore _cache;
        private readonly ILogger<LicenseClient>? _log;
        private readonly bool _ownsHttp;
        private bool _disposed;

        private SecurityKey? _signingKey;
        private bool _signingKeyResolved;

        /// <summary>The current license state snapshot (never null).</summary>
        public LicenseState Current { get; private set; } = LicenseState.Empty;

        /// <summary>The configured options (read-only view).</summary>
        public TollgateOptions Options => _options;

        // ── Constructors ─────────────────────────────────────────

        /// <summary>
        /// Convenience constructor for non-DI apps (Console, WinForms, WPF).
        /// Creates and owns an <see cref="HttpClient"/>.
        /// </summary>
        public LicenseClient(TollgateOptions options, ILogger<LicenseClient>? log = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options;
            _log = log;
            _cache = new LicenseStore(_options);
            _http = new HttpClient();
            _ownsHttp = true;
            ConfigureHttp(_http);
        }

        /// <summary>
        /// Advanced: use a pre-built <see cref="HttpClient"/> (e.g. a typed
        /// client from IHttpClientFactory). The caller keeps ownership of the
        /// instance; it is not disposed by this client.
        /// </summary>
        public LicenseClient(HttpClient http, IOptions<TollgateOptions> options,
                             ILogger<LicenseClient>? log = null)
        {
            ArgumentNullException.ThrowIfNull(http);
            ArgumentNullException.ThrowIfNull(options);
            _options = options.Value;
            _log = log;
            _cache = new LicenseStore(_options);
            _http = http;
            _ownsHttp = false;
            ConfigureHttp(http);
        }

        /// <summary>
        /// DI constructor — rents a fresh <see cref="HttpClient"/> from
        /// <paramref name="httpFactory"/> per operation so the singleton
        /// client does not pin a transient handler (no captive dependency).
        /// </summary>
        public LicenseClient(IHttpClientFactory httpFactory, IOptions<TollgateOptions> options,
                             ILogger<LicenseClient>? log = null)
        {
            ArgumentNullException.ThrowIfNull(httpFactory);
            ArgumentNullException.ThrowIfNull(options);
            _options = options.Value;
            _log = log;
            _cache = new LicenseStore(_options);
            _httpFactory = httpFactory;
            _http = null;
            _ownsHttp = false;
        }

        // ── Startup: load saved license ──────────────────────────

        /// <inheritdoc />
        public async Task<bool> TryLoadSavedLicenseAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var cached = _cache.Load();
            if (cached is null) return false;

            // Verify the cached JWT locally — full signature, issuer,
            // audience, lifetime, machine + app binding. Claims inside the
            // verified token (not the cache file) are the source of truth
            // for tier and features.
            bool tokenValid = TryValidateToken(cached.Token, cached.ExpiresAt,
                                               cached.LicenseKey, out var state);

            // Offline grace: within the window, a verified token is honored
            // with no network call. The token's own expiry (issued by the
            // server) is the hard cryptographic limit.
            var graceDays = Math.Max(0, _options.OfflineGraceDays);
            bool withinGrace = cached.CachedAt.AddDays(graceDays) >= DateTime.UtcNow;

            if (tokenValid && withinGrace)
            {
                Current = state!;
                _log?.LogInformation("License loaded from verified local cache (tier={Tier}, {Days}d grace).",
                    state!.Tier, graceDays);
                return true;
            }

            // No valid cached token within grace (expired, beyond grace, or
            // no signing key configured) — re-validate online.
            var (response, serverUnreachable) =
                await ActivateCoreAsync(cached.LicenseKey, cancellationToken).ConfigureAwait(false);

            if (response.IsValid) return true;

            if (!serverUnreachable)
            {
                // The server authoritatively rejected the key (revoked,
                // expired, machine mismatch). Purge the cache.
                _log?.LogWarning("License rejected by server: {Message}. Clearing cached license.",
                    response.Message);
                Current = LicenseState.Empty;
                _cache.Clear();
            }
            // Server unreachable: keep the cache for the next launch, but do
            // NOT honor a beyond-grace token offline — return false and let
            // the app fall back according to its AllowFreeMode policy.
            return false;
        }

        // ── Activate a key ───────────────────────────────────────

        /// <inheritdoc />
        public async Task<ValidateLicenseResponse> ActivateKeyAsync(
            string licenseKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var (response, _) = await ActivateCoreAsync(licenseKey, cancellationToken).ConfigureAwait(false);
            return response;
        }

        // ── Deactivate: release machine binding on the server ────

        /// <inheritdoc />
        public async Task<ValidateLicenseResponse> DeactivateAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var key = (Current.IsValid && !string.IsNullOrEmpty(Current.LicenseKey))
                ? Current.LicenseKey
                : _cache.Load()?.LicenseKey;
            if (string.IsNullOrEmpty(key))
                return new ValidateLicenseResponse
                {
                    IsValid = false,
                    Message = "No license to deactivate."
                };

            try
            {
                var request = new DeactivateLicenseRequest
                {
                    LicenseKey = key.Trim().ToUpperInvariant(),
                    MachineId  = MachineFingerprint.Get(),
                    AppId      = _options.AppId
                };

                var http = RentHttp();
                HttpResponseMessage res;
                try
                {
                    res = await http.PostAsJsonAsync("/api/license/deactivate", request, cancellationToken)
                                    .ConfigureAwait(false);
                }
                finally { ReturnHttp(http); }

                var body = TryDeserialize(await res.Content.ReadAsStringAsync(cancellationToken)
                                                .ConfigureAwait(false));

                if (body?.IsValid == true)
                {
                    Current = LicenseState.Empty;
                    _cache.Clear();
                    _log?.LogInformation("License {Key} deactivated on server; local cache cleared.", key);
                }
                return body ?? new ValidateLicenseResponse
                {
                    IsValid = false,
                    Message = "Empty server response."
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // caller-initiated cancellation propagates
            }
            catch (HttpRequestException ex)
            {
                return Unreachable(ex.Message);
            }
            catch (TaskCanceledException)
            {
                return Unreachable($"no response within {_options.HttpTimeout.TotalSeconds:0}s");
            }
        }

        // ── Local (verified) token validation ────────────────────

        /// <summary>
        /// Validate a token with full cryptographic rigor. Returns false when
        /// no signing key is configured (fail closed), when the signature,
        /// issuer, audience, lifetime, machine or app binding fails, or when
        /// the tier claim is not a defined enum value.
        /// </summary>
        private bool TryValidateToken(string token, DateTime? licenseExpiry,
                                      string licenseKey, out LicenseState? state)
        {
            state = null;
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                var key = ResolveSigningKey();
                if (key is null)
                {
                    _log?.LogWarning(
                        "Tollgate: neither PublicKey nor SharedSecret is configured — " +
                        "offline token verification is disabled (fail closed). " +
                        "Configure PublicKey (recommended) or SharedSecret to enable it.");
                    return false;
                }

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = _options.Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = _options.Audience,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = key,
                    ClockSkew                = TimeSpan.FromMinutes(5)
                }, out var validatedToken);

                // Machine binding — must be present and match THIS machine.
                var mid = principal.FindFirst("mid")?.Value ?? "";
                if (mid.Length == 0 || mid != MachineFingerprint.Get()) return false;

                // App binding — a token minted for another product must not
                // validate here.
                var app = principal.FindFirst("app")?.Value ?? "";
                if (app.Length > 0 && app != _options.AppId) return false;

                // Tier — parse case-insensitively and require a defined value.
                var tierStr = principal.FindFirst("tier")?.Value ?? "";
                if (!Enum.TryParse(tierStr, ignoreCase: true, out LicenseTier tier) ||
                    !Enum.IsDefined(tier))
                    return false;

                var features = (principal.FindFirst("feat")?.Value ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var jwt = validatedToken as JwtSecurityToken;

                state = new LicenseState
                {
                    IsValid    = true,
                    Tier       = tier,
                    LicenseKey = principal.FindFirst("lic")?.Value ?? licenseKey,
                    AppId      = app,
                    MachineId  = mid,
                    ExpiresAt  = licenseExpiry ?? jwt?.ValidTo,
                    Features   = features,
                    CheckedAt  = DateTime.UtcNow,
                    Message    = "Loaded from verified local cache."
                };
                return true;
            }
            catch (Exception ex)
            {
                _log?.LogDebug("Tollgate: token validation failed ({Type}: {Message}).",
                    ex.GetType().Name, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Resolve the verification key from options: RSA public key (PEM,
        /// XML or base64 DER) when configured, otherwise the HMAC shared
        /// secret, otherwise null (offline verification disabled).
        /// </summary>
        private SecurityKey? ResolveSigningKey()
        {
            if (_signingKeyResolved) return _signingKey;
            _signingKey = BuildSigningKey();
            _signingKeyResolved = true;
            return _signingKey;
        }

        private SecurityKey? BuildSigningKey()
        {
            var publicKey = _options.PublicKey?.Trim();
            if (!string.IsNullOrEmpty(publicKey))
            {
                RSA? rsa = null;
                try
                {
                    rsa = RSA.Create();
                    if (publicKey.Contains("-----BEGIN"))
                        rsa.ImportFromPem(publicKey);
                    else if (publicKey.Contains("<RSAKeyValue"))
                        rsa.FromXmlString(publicKey);
                    else
                        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey));

                    return new RsaSecurityKey(rsa);
                }
                catch (Exception ex)
                {
                    rsa?.Dispose();
                    _log?.LogError(
                        "Tollgate: the configured PublicKey could not be parsed ({Message}). " +
                        "Falling back to SharedSecret verification if one is configured.", ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(_options.SharedSecret))
                return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SharedSecret));

            return null;
        }

        // ── Online activation core ───────────────────────────────

        private async Task<(ValidateLicenseResponse Response, bool ServerUnreachable)> ActivateCoreAsync(
            string licenseKey, CancellationToken cancellationToken)
        {
            var key = licenseKey.Trim().ToUpperInvariant();
            try
            {
                var request = new ValidateLicenseRequest
                {
                    LicenseKey = key,
                    MachineId  = MachineFingerprint.Get(),
                    AppId      = _options.AppId,
                    AppVersion = _options.AppVersion
                };

                var http = RentHttp();
                HttpResponseMessage res;
                try
                {
                    res = await http.PostAsJsonAsync("/api/license/validate", request, cancellationToken)
                                    .ConfigureAwait(false);
                }
                finally { ReturnHttp(http); }

                var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var body = TryDeserialize(raw);

                if (body?.IsValid == true)
                {
                    // Defense in depth: when a verification key is configured,
                    // verify the token the server just returned before trusting
                    // it — blocks a man-in-the-middle substituting its own
                    // response on a plain-HTTP connection.
                    if (ResolveSigningKey() is not null &&
                        !TryValidateToken(body.Token, body.ExpiresAt, key, out _))
                    {
                        _log?.LogError(
                            "Tollgate: the server returned a token that failed signature " +
                            "verification — activation rejected.");
                        return (new ValidateLicenseResponse
                        {
                            IsValid = false,
                            Message = "License server response failed integrity verification."
                        }, false);
                    }

                    Current = new LicenseState
                    {
                        IsValid    = true,
                        Tier       = body.Tier,
                        LicenseKey = key,
                        AppId      = body.AppId,
                        MachineId  = request.MachineId,
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

                    _log?.LogInformation("License activated: app={App} tier={Tier} features={Features}",
                        body.AppId, body.Tier, string.Join(",", body.Features));
                }
                else
                {
                    _log?.LogWarning("License activation failed: app={App} key={Key} message={Message}",
                        _options.AppId, key, body?.Message ?? "(empty response)");
                }

                return (body ?? new ValidateLicenseResponse
                {
                    IsValid = false,
                    Message = "Empty server response."
                }, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // caller-initiated cancellation propagates
            }
            catch (HttpRequestException ex)
            {
                return (Unreachable(ex.Message), true);
            }
            catch (TaskCanceledException)
            {
                return (Unreachable($"no response within {_options.HttpTimeout.TotalSeconds:0}s"), true);
            }
            catch (Exception ex)
            {
                // Unknown transport-level failure — treat as unreachable so
                // the cache is never purged because of a local problem.
                return (Unreachable(ex.Message), true);
            }
        }

        // ── Helpers ──────────────────────────────────────────────

        private ValidateLicenseResponse Unreachable(string why) =>
            new() { IsValid = false, Message = $"Cannot reach license server: {why}" };

        private static ValidateLicenseResponse? TryDeserialize(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try { return JsonSerializer.Deserialize<ValidateLicenseResponse>(raw, JsonOpts); }
            catch (JsonException) { return null; }
        }

        private void ConfigureHttp(HttpClient http)
        {
            try
            {
                if (http.BaseAddress is null)
                    http.BaseAddress = new Uri(_options.ServerUrl.TrimEnd('/'));
                http.Timeout = _options.HttpTimeout;
            }
            catch (InvalidOperationException)
            {
                // The client has already issued a request — its BaseAddress
                // and Timeout can no longer be changed; its owner manages
                // them. Leave everything as configured.
            }
        }

        private HttpClient RentHttp()
        {
            if (_httpFactory is not null)
            {
                var client = _httpFactory.CreateClient(HttpClientName);
                ConfigureHttp(client);
                return client;
            }
            return _http!;
        }

        private void ReturnHttp(HttpClient client)
        {
            // Only dispose clients rented from the factory. The owned /
            // injected instance lives as long as this LicenseClient.
            if (_httpFactory is not null) client.Dispose();
        }

        private void ThrowIfDisposed() =>
            ObjectDisposedException.ThrowIf(_disposed, this);

        /// <inheritdoc />
        public void ClearLicense()
        {
            Current = LicenseState.Empty;
            _cache.Clear();
        }

        // ── IDisposable ──────────────────────────────────────────

        /// <summary>
        /// Disposes the underlying <see cref="HttpClient"/> only when this
        /// client created it. Factory-rented and caller-injected clients are
        /// owned elsewhere and left alone.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsHttp) _http?.Dispose();
        }
    }
}
