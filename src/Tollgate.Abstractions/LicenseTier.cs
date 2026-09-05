namespace Tollgate;

// ─────────────────────────────────────────────────────────────
//  LICENSE TIERS
//
//  Tiers are an *ordered* convenience. Higher tiers implicitly
//  include lower tiers. A license can ALSO have explicit
//  features (arbitrary string tags), so you are not forced into
//  a rigid 3-tier model — you can mix both.
//
//  None    = no license / trial / free mode
//  Basic   = paid starter tier
//  Pro     = paid pro tier  (>= Basic)
//  Enterprise = paid enterprise tier (>= Pro)
// ─────────────────────────────────────────────────────────────

/// <summary>License tiers. Ordered: higher values include lower ones.</summary>
public enum LicenseTier
{
    None       = 0,
    Free       = 1,
    Basic      = 2,
    Pro        = 3,
    Enterprise = 4
}

/// <summary>
/// Convenience helpers for comparing tiers.
/// </summary>
public static class LicenseTiers
{
    /// <summary>True if <paramref name="actual"/> is at least <paramref name="required"/>.</summary>
    public static bool Meets(LicenseTier actual, LicenseTier required) =>
        (int)actual >= (int)required;
}
