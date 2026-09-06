using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Admin: request body for POST /api/admin/reset-machine.
    /// Authentication is via the X-Admin-Key header, not the body.
    /// </summary>
    public record ResetMachineRequest
    {
        /// <summary>The license key whose machine binding should be cleared.</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>Optional app scope (recommended - keys are unique per app).</summary>
        public string? AppId { get; init; }
    }
}
