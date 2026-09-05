using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions
{
    /// <summary>
    /// Convenience helpers for comparing tiers.
    /// </summary>
    public static class LicenseTiers
    {
        /// <summary>True if <paramref name="actual"/> is at least <paramref name="required"/>.</summary>
        public static bool Meets(LicenseTier actual, LicenseTier required) =>
            (int)actual >= (int)required;
    }
}
