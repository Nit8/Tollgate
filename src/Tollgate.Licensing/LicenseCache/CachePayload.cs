using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Licensing.LicenseCache
{
    // ── On-disk JSON shape (encrypted as a whole) ───────────
    internal sealed class CachePayload
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
