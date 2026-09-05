using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Admin: list all keys for an app.</summary>
    public record LicenseKeyInfo
    {
        public string LicenseKey { get; init; } = "";
        public string AppId { get; init; } = "";
        public LicenseTier Tier { get; init; }
        public List<string> Features { get; init; } = new();
        public bool IsActive { get; init; }
        public string? MachineId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ActivatedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public int UseCount { get; init; }
        public string? Notes { get; init; }
    }
}
