using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions
{
    /// <summary>
    /// A snapshot of the currently active license. This is what
    /// <c>LicenseGate.Current</c> exposes to application code.
    /// </summary>
    public sealed class LicenseState
    {
        /// <summary>True if any non-None license is active.</summary>
        public bool IsValid { get; init; }

        /// <summary>The active tier (None if no license).</summary>
        public LicenseTier Tier { get; init; } = LicenseTier.None;

        /// <summary>The license key (for display / support purposes).</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>The app/product this license belongs to.</summary>
        public string AppId { get; init; } = "";

        /// <summary>Machine ID bound to this license.</summary>
        public string MachineId { get; init; } = "";

        /// <summary>Optional expiry date (UTC). Null = lifetime license.</summary>
        public DateTime? ExpiresAt { get; init; }

        /// <summary>
        /// Explicit feature list attached to this license.
        /// May be populated even on a None-tier trial key.
        /// </summary>
        public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();

        /// <summary>When this state was last refreshed (locally or online).</summary>
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

        /// <summary>Human-readable status message from the server.</summary>
        public string Message { get; init; } = "";

        // ── Convenience predicates ─────────────────────────────────

        public bool IsLicensed => Tier != LicenseTier.None && IsValid;
        public bool IsTrial => IsValid && Tier == LicenseTier.None;
        public bool IsFree => Tier == LicenseTier.Free;
        public bool IsBasic => Tier == LicenseTier.Basic;
        public bool IsPro => Tier == LicenseTier.Pro;
        public bool IsEnterprise => Tier == LicenseTier.Enterprise;

        public static LicenseState Empty => new();
    }

}