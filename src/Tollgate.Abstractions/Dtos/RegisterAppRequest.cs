using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    public record RegisterAppRequest
    {
        public string AppId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string AdminKey { get; init; } = "";
    }
}
