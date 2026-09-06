using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Admin: request body for POST /api/admin/apps/register.
    /// Authentication is via the X-Admin-Key header, not the body.
    /// </summary>
    public record RegisterAppRequest
    {
        /// <summary>The new application's unique ID.</summary>
        public string AppId { get; init; } = "";

        /// <summary>Optional human-friendly display name (defaults to AppId).</summary>
        public string DisplayName { get; init; } = "";
    }
}
