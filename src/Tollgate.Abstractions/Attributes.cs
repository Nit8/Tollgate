using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions
{
    // ─────────────────────────────────────────────────────────────
    //  DATA ANNOTATIONS — the heart of Tollgate's developer API
    //
    //  Drop [RequireFeature("export-pdf")] on a method, class,
    //  controller action, or property, and Tollgate will block
    //  callers that do not have that feature on their license.
    //
    //  Enforcement is automatic for ASP.NET Core (via a global
    //  action filter registered by services.AddTollgate(...)).
    //
    //  For WPF / WinForms / Console, call:
    //
    //      LicenseGate.EnsureFeature("export-pdf");
    //
    //  …or use the reflection helper:
    //
    //      LicenseGate.EnsureAccessFor(currentMethod);
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks a method, class, or property as requiring a specific
    /// feature on the active license. The feature name is an
    /// arbitrary string (e.g. "export-pdf", "ai-assistant")
    /// assigned to a license key on the server side.
    /// </summary>
    /// <example>
    /// <code>
    /// public class TodoService
    /// {
    ///     [RequireFeature("export-pdf")]
    ///     public byte[] ExportToPdf() { /* ... */ }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Method |
        AttributeTargets.Class |
        AttributeTargets.Property |
        AttributeTargets.Struct,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class RequireFeatureAttribute : Attribute
    {
        /// <summary>The feature name required to access this member.</summary>
        public string Feature { get; }

        /// <summary>Optional message shown to the user when access is denied.</summary>
        public string? DeniedMessage { get; set; }

        /// <summary>
        /// Marks the member as requiring the named feature.
        /// </summary>
        /// <param name="feature">Feature name assigned to the license key server-side (e.g. "export-pdf").</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="feature"/> is null or blank.</exception>
        public RequireFeatureAttribute(string feature)
        {
            if (string.IsNullOrWhiteSpace(feature))
                throw new ArgumentException("Feature name is required.", nameof(feature));
            Feature = feature.Trim();
        }
    }

    /// <summary>
    /// Marks a method, class, or property as requiring a minimum
    /// tier on the active license. Tier ordering is implicit
    /// (Pro &gt; Basic &gt; Free &gt; None).
    /// </summary>
    /// <example>
    /// <code>
    /// [RequireTier(LicenseTier.Pro)]
    /// public void BulkImport() { /* ... */ }
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Method |
        AttributeTargets.Class |
        AttributeTargets.Property |
        AttributeTargets.Struct,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class RequireTierAttribute : Attribute
    {
        /// <summary>The minimum tier required to access this member.</summary>
        public LicenseTier Tier { get; }

        /// <summary>Optional message shown to the user when access is denied.</summary>
        public string? DeniedMessage { get; set; }

        /// <summary>
        /// Marks the member as requiring at least the given tier.
        /// </summary>
        /// <param name="tier">Minimum tier required (higher tiers implicitly pass).</param>
        public RequireTierAttribute(LicenseTier tier)
        {
            Tier = tier;
        }
    }

    /// <summary>
    /// Marks a member as available only during a trial — the license must be
    /// valid AND its tier must be <see cref="Enums.LicenseTier.None"/> (trial
    /// keys are issued as None-tier keys, e.g. TRIAL-XXXX-...).
    /// Enforced by <c>LicenseGate.EnsureAccessFor(...)</c>, the ASP.NET Core
    /// <c>RequireFeatureFilter</c>, and <c>LicenseGate.EnsureTrial()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// [RequireTrial(DeniedMessage = "This feature is part of the trial.")]
    /// public void PreviewFeature() { /* ... */ }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class,
                    AllowMultiple = false, Inherited = true)]
    public sealed class RequireTrialAttribute : Attribute
    {
        /// <summary>Optional message shown to the user when access is denied.</summary>
        public string? DeniedMessage { get; set; }
    }
}