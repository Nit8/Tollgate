using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Admin: generate keys request.</summary>
    public record GenerateKeysRequest
    {
        public string AppId { get; init; } = "";
        public LicenseTier Tier { get; init; }
        public List<string> Features { get; init; } = new();
        public int Count { get; init; } = 1;
        public int? ValidDays { get; init; }
        public string AdminKey { get; init; } = "";
        public string? Notes { get; init; }
    }
}
