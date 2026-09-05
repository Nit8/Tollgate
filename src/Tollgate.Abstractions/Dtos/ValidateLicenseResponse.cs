using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Server replies with this.</summary>
    public record ValidateLicenseResponse
    {
        public bool IsValid { get; init; }
        public LicenseTier Tier { get; init; } = LicenseTier.None;
        public List<string> Features { get; init; } = new();
        public string Message { get; init; } = "";
        public DateTime? ExpiresAt { get; init; }
        public string Token { get; init; } = "";
        public string AppId { get; init; } = "";
    }
}
