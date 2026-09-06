using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions
{
    /// <summary>
    /// Thrown when a feature/tier check fails. Catch this in
    /// your UI layer to redirect users to an upgrade screen.
    /// </summary>
    public class LicenseRequiredException : Exception
    {
        /// <summary>The feature that was required (if any).</summary>
        public string? Feature { get; }

        /// <summary>The tier that was required (if any).</summary>
        public LicenseTier RequiredTier { get; }

        /// <summary>The current tier of the user (if known).</summary>
        public LicenseTier CurrentTier { get; }

        /// <summary>Creates the exception for a failed feature check.</summary>
        /// <param name="feature">The feature name that was required.</param>
        /// <param name="current">The caller's current tier, if known.</param>
        public LicenseRequiredException(string feature, LicenseTier current = LicenseTier.None)
            : base($"This action requires the '{feature}' feature. " +
                   $"Upgrade your license to unlock it.")
        {
            Feature = feature;
            RequiredTier = LicenseTier.None;
            CurrentTier = current;
        }

        /// <summary>Creates the exception for a failed tier check.</summary>
        /// <param name="required">The minimum tier that was required.</param>
        /// <param name="current">The caller's current tier, if known.</param>
        public LicenseRequiredException(LicenseTier required, LicenseTier current = LicenseTier.None)
            : base($"This action requires at least the '{required}' tier. " +
                   $"Your current tier is '{current}'. Upgrade to unlock it.")
        {
            Feature = null;
            RequiredTier = required;
            CurrentTier = current;
        }

        /// <summary>Creates the exception with a custom message.</summary>
        /// <param name="message">The message to surface to the caller.</param>
        public LicenseRequiredException(string message) : base(message)
        {
            RequiredTier = LicenseTier.None;
            CurrentTier = LicenseTier.None;
        }
    }

    /// <summary>
    /// Thrown when no license is configured at all — the app is
    /// running in "free mode" and the developer can decide how
    /// to react (show activation dialog, block the action, etc.).
    /// </summary>
    public class LicenseNotConfiguredException : Exception
    {
        /// <summary>Creates the exception with an optional custom message.</summary>
        /// <param name="message">The message to surface to the caller.</param>
        public LicenseNotConfiguredException(string message = "No license configured.")
            : base(message) { }
    }
}
