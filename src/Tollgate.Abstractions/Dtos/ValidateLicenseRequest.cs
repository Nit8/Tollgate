using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Client sends this when validating a key.</summary>
    public record ValidateLicenseRequest
    {
        public string LicenseKey { get; init; } = "";
        public string MachineId { get; init; } = "";
        public string AppId { get; init; } = "";
        public string AppVersion { get; init; } = "1.0.0";
    }
}
