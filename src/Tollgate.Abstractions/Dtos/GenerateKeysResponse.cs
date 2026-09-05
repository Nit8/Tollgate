using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    public record GenerateKeysResponse
    {
        public List<string> Keys { get; init; } = new();
        public string Message { get; init; } = "";
    }
}
