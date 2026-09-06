using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Admin: request body for POST /api/admin/set-features.
    /// Authentication is via the X-Admin-Key header, not the body.
    /// </summary>
    public record SetFeaturesRequest
    {
        /// <summary>The license key to update.</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>The app the key belongs to.</summary>
        public string AppId { get; init; } = "";

        /// <summary>The full replacement feature list for the key.</summary>
        public List<string> Features { get; init; } = new();
    }
}
