using Tollgate.Abstractions;
using Tollgate.Abstractions.Dtos;

namespace Tollgate.Licensing.Interfaces
{
    /// <summary>
    /// Interface for the license client (useful for tests / DI).
    /// Implemented by <see cref="LicenseClient"/>.
    /// </summary>
    public interface ILicenseClient
    {
        /// <summary>The current license state snapshot (never null).</summary>
        LicenseState Current { get; }

        /// <summary>The configured options (read-only view).</summary>
        TollgateOptions Options { get; }

        /// <summary>
        /// Try to load a previously-activated license from the encrypted disk
        /// cache. Tokens are cryptographically verified (signature, issuer,
        /// audience, expiry, machine and app binding) before being honored.
        /// Within the offline grace window no network call is made; beyond it,
        /// the key is re-validated online.
        /// </summary>
        /// <returns>True if a valid (cached or re-validated) license was found.</returns>
        Task<bool> TryLoadSavedLicenseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Activate a license key online. On success, <see cref="Current"/> is
        /// updated and the signed token is cached (encrypted) for offline use.
        /// When a signing key is configured, the returned token is verified
        /// before it is trusted.
        /// </summary>
        Task<ValidateLicenseResponse> ActivateKeyAsync(string licenseKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Release the machine binding for the current license on the server
        /// (allows moving the license to another machine), then clear the
        /// local cache. Requires the server to be reachable.
        /// </summary>
        Task<ValidateLicenseResponse> DeactivateAsync(CancellationToken cancellationToken = default);

        /// <summary>Remove the cached license and reset state (local only).</summary>
        void ClearLicense();
    }
}
