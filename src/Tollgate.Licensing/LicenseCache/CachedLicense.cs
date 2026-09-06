using System;
using System.Collections.Generic;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Licensing.LicenseCache
{
    /// <summary>In-memory representation of a cached license.</summary>
    public sealed class CachedLicense
    {
        /// <summary>The license key that produced the token.</summary>
        public string LicenseKey { get; set; } = "";

        /// <summary>The signed JWT issued by the server.</summary>
        public string Token { get; set; } = "";

        /// <summary>The app the license belongs to.</summary>
        public string AppId { get; set; } = "";

        /// <summary>License expiry (UTC) — null for lifetime licenses.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>When the cache entry was written (drives the grace window).</summary>
        public DateTime CachedAt { get; set; }

        /// <summary>Feature list mirrored from the token (display only — claims are authoritative).</summary>
        public List<string> Features { get; set; } = new();

        /// <summary>Tier mirrored from the token (display only — claims are authoritative).</summary>
        public LicenseTier Tier { get; set; } = LicenseTier.None;
    }
}
