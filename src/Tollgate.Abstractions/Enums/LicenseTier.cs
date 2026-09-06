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
        /// <summary>No license, trial, or free mode.</summary>
        None = 0,

        /// <summary>Registered free tier (no paid features).</summary>
        Free = 1,

        /// <summary>Paid starter tier.</summary>
        Basic = 2,

        /// <summary>Paid pro tier; includes Basic.</summary>
        Pro = 3,

        /// <summary>Paid enterprise tier; includes Pro.</summary>
        Enterprise = 4
    }
}

