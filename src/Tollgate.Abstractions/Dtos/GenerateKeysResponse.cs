using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Admin: response for POST /api/admin/generate.</summary>
    public record GenerateKeysResponse
    {
        /// <summary>The generated license keys.</summary>
        public List<string> Keys { get; init; } = new();

        /// <summary>Human-readable summary of the operation.</summary>
        public string Message { get; init; } = "";
    }
}
