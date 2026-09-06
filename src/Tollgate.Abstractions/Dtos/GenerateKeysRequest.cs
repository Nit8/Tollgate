using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Admin: request body for POST /api/admin/generate.
    /// Authentication is via the X-Admin-Key header, not the body.
    /// </summary>
    public record GenerateKeysRequest
    {
        /// <summary>The app the keys belong to (auto-registered unless Apps:AllowAutoRegister is false).</summary>
        public string AppId { get; init; } = "";

        /// <summary>The tier to mint the keys for.</summary>
        public LicenseTier Tier { get; init; }

        /// <summary>Explicit feature flags attached to each key (in addition to the tier).</summary>
        public List<string> Features { get; init; } = new();

        /// <summary>How many keys to generate (1-100).</summary>
        public int Count { get; init; } = 1;

        /// <summary>Optional validity window in days; null = lifetime keys.</summary>
        public int? ValidDays { get; init; }

        /// <summary>Optional internal notes stored with each key.</summary>
        public string? Notes { get; init; }
    }
}
