using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Enums
{
    
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
        None = 0,
        Free = 1,
        Basic = 2,
        Pro = 3,
        Enterprise = 4
    }
}

