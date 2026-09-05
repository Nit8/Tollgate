using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Server.Services
{
    // ─────────────────────────────────────────────────────────────
    //  TOKEN SERVICE
    //
    //  Issues signed JWT tokens the client caches locally. The token
    //  contains the tier AND the list of features, so offline clients
    //  can enforce [RequireFeature] without re-validating online.
    //
    //  Signing: HMAC-SHA256 by default (symmetric — works out of the
    //  box with the Jwt:Secret setting). For RSA (asymmetric), set
    //  Jwt:PublicKey / Jwt:PrivateKey in config and the server will
    //  sign with RSA and the client verifies with only the public key.
    // ─────────────────────────────────────────────────────────────

    public class TokenService
    {
        private readonly IConfiguration _cfg;
        private readonly SigningCredentials? _rsaCreds;

        public TokenService(IConfiguration cfg)
        {
            _cfg = cfg;

            // Try RSA first if both keys are present.
            var privPem = _cfg["Jwt:PrivateKey"];
            if (!string.IsNullOrEmpty(privPem))
            {
                try
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(privPem);
                    _rsaCreds = new SigningCredentials(
                        new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
                }
                catch { /* fall back to HMAC */ }
            }
        }

        /// <summary>
        /// Issue a signed JWT with tier + features as claims.
        /// </summary>
        public string IssueToken(
            string licenseKey, string appId,
            LicenseTier tier, List<string> features,
            string machineId, DateTime? licenseExpiry)
        {
            var lifetimeDays = int.TryParse(_cfg["Jwt:TokenLifetimeDays"], out var d) ? d : 7;
            var tokenExpiry = DateTime.UtcNow.AddDays(lifetimeDays);

            // Cap token at license expiry
            if (licenseExpiry.HasValue && licenseExpiry.Value < tokenExpiry)
                tokenExpiry = licenseExpiry.Value;

            var issuer = _cfg["Jwt:Issuer"] ?? "TollgateServer";
            var audience = _cfg["Jwt:Audience"] ?? "TollgateClient";

            var claims = new List<Claim>
        {
            new("lic", licenseKey),
            new("app", appId),
            new("tier", tier.ToString()),
            new("mid", machineId),
            new("feat", string.Join(",", features)),
        };

            var creds = GetSigningCredentials();
            var jwt = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: tokenExpiry,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }

        /// <summary>Validate a cached token. Returns null if invalid.</summary>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var key = GetSigningKey();
                var handler = new JwtSecurityTokenHandler();
                return handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _cfg["Jwt:Issuer"] ?? "TollgateServer",
                    ValidateAudience = true,
                    ValidAudience = _cfg["Jwt:Audience"] ?? "TollgateClient",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromMinutes(5)
                }, out _);
            }
            catch { return null; }
        }

        // ── Signing credential / key resolution ──────────────────
        private SigningCredentials GetSigningCredentials()
        {
            if (_rsaCreds is not null) return _rsaCreds;

            var secret = _cfg["Jwt:Secret"]
                         ?? throw new InvalidOperationException("Jwt:Secret not configured");
            if (secret.Length < 32)
                throw new InvalidOperationException("Jwt:Secret must be >= 32 chars for HMAC-SHA256.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        }

        private SecurityKey GetSigningKey()
        {
            if (_rsaCreds is not null) return _rsaCreds.Key;
            var secret = _cfg["Jwt:Secret"] ?? "";
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        }
    }
}
