using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Admin: paginated response for GET /api/admin/keys.</summary>
    public record KeyListResponse
    {
        /// <summary>The keys on the requested page.</summary>
        public List<LicenseKeyInfo> Keys { get; init; } = new();

        /// <summary>1-based page number.</summary>
        public int Page { get; init; }

        /// <summary>Page size actually applied.</summary>
        public int PageSize { get; init; }

        /// <summary>Total keys matching the filters (across all pages).</summary>
        public int Total { get; init; }
    }
}
