using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Admin: request body for POST /api/admin/revoke.
    /// Authentication is via the X-Admin-Key header, not the body.
    /// </summary>
    public record RevokeKeyRequest
    {
        /// <summary>The license key to revoke.</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>Optional app scope (recommended - keys are unique per app).</summary>
        public string? AppId { get; init; }
    }
}
