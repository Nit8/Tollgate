using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Licensing.LicenseCache
{
    internal sealed class LicenseCache
    {
        // ─────────────────────────────────────────────────────────────
        //  ENCRYPTED LOCAL CACHE
        //
        //  Stores the JWT cache token + license metadata on disk so
        //  users don't have to re-enter their key on every launch.
        //
        //  Windows  → DPAPI (user-bound, AES via CryptProtectData)
        //  Linux/Mac→ AES-GCM with a key derived from the machine ID
        //             (less secure than DPAPI but better than plaintext).
        //
        //  The cache file lives in:
        //   - %LOCALAPPDATA%/Tollgate/<AppId>/license.dat   (Windows)
        //   - ~/.local/share/Tollgate/<AppId>/license.dat   (Linux)
        //   - ~/Library/Application Support/Tollgate/<AppId>/license.dat  (macOS)
        // ─────────────────────────────────────────────────────────────
        private readonly string _path;

        public LicenseCache(TollgateOptions options)
        {
            var dir = !string.IsNullOrWhiteSpace(options.CacheDirectory)
                ? options.CacheDirectory
                : Path.Combine(GetAppDataRoot(), "Tollgate", SanitizeAppId(options.AppId));
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, options.CacheFile);
        }

        public CachedLicense? Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var bytes = File.ReadAllBytes(_path);
                var plain = Unprotect(bytes);
                var json = Encoding.UTF8.GetString(plain);
                var p = JsonSerializer.Deserialize<CachePayload>(json);
                if (p is null) return null;

                return new CachedLicense
                {
                    LicenseKey = p.LicenseKey,
                    Token = p.Token,
                    AppId = p.AppId,
                    ExpiresAt = p.ExpiresAt,
                    CachedAt = p.CachedAt,
                    Features = p.Features,
                    Tier = p.Tier,
                };
            }
            catch { return null; }
        }

        public void Save(CachedLicense license)
        {
            try
            {
                var p = new CachePayload
                {
                    LicenseKey = license.LicenseKey,
                    Token = license.Token,
                    AppId = license.AppId,
                    ExpiresAt = license.ExpiresAt,
                    CachedAt = DateTime.UtcNow,
                    Features = license.Features,
                    Tier = license.Tier,
                };
                var json = JsonSerializer.Serialize(p);
                var plain = Encoding.UTF8.GetBytes(json);
                var cipher = Protect(plain);
                File.WriteAllBytes(_path, cipher);
            }
            catch { /* non-fatal */ }
        }

        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch { }
        }

        // ── Cross-platform protect / unprotect ───────────────────

        private static byte[] Protect(byte[] plain)
        {
#if NET10_0_WINDOWS
    if (OperatingSystem.IsWindows()) return ProtectDpapi(plain);
#endif
            return ProtectAesGcm(plain);
        }

        private static byte[] Unprotect(byte[] cipher)
        {
#if NET10_0_WINDOWS
    if (OperatingSystem.IsWindows()) return UnprotectDpapi(cipher);
#endif
            return UnprotectAesGcm(cipher);
        }

#if NET10_0_WINDOWS
// ── DPAPI (Windows only — uses System.Security.Cryptography.ProtectedData) ──
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
private static byte[] ProtectDpapi(byte[] plain) =>
    ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
private static byte[] UnprotectDpapi(byte[] cipher) =>
    ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
#endif

        // ── AES-GCM fallback (Linux / macOS) ─────────────────────
        //  Layout:  [12-byte nonce | 16-byte tag | ciphertext]
        //  Key derived from machine fingerprint + a fixed app salt.
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private static readonly byte[] AppSalt = Encoding.UTF8.GetBytes("Tollgate.Cache.v1");

        private static byte[] DeriveKey()
        {
            var fingerprint = MachineFingerprint.Get();
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(fingerprint), AppSalt,
                iterations: 50_000, HashAlgorithmName.SHA256, outputLength: 32);
        }

        private static byte[] ProtectAesGcm(byte[] plain)
        {
            var key = DeriveKey();
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[plain.Length];

            using var gcm = new AesGcm(key, TagSize);
            gcm.Encrypt(nonce, plain, cipher, tag);

            var result = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);
            return result;
        }

        private static byte[] UnprotectAesGcm(byte[] data)
        {
            if (data.Length < NonceSize + TagSize) throw new CryptographicException("Too short");
            var key = DeriveKey();
            var nonce = data[..NonceSize];
            var tag = data[NonceSize..(NonceSize + TagSize)];
            var cipher = data[(NonceSize + TagSize)..];
            var plain = new byte[cipher.Length];

            using var gcm = new AesGcm(key, TagSize);
            gcm.Decrypt(nonce, cipher, tag, plain);
            return plain;
        }

        // ── App-data root per OS ─────────────────────────────────
        private static string GetAppDataRoot() =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private static string SanitizeAppId(string appId)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in appId)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }
        
    }
}
