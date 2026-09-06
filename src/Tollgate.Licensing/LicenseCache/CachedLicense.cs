using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Licensing.LicenseCache
{
    /// <summary>In-memory representation of a cached license.</summary>
    public sealed class CachedLicense
    {
        public string LicenseKey { get; set; } = "";
        public string Token { get; set; } = "";
        public string AppId { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
        public DateTime CachedAt { get; set; }
        public List<string> Features { get; set; } = new();
        public LicenseTier Tier { get; set; } = LicenseTier.None;
    }
}
