using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Public: client request for POST /api/license/validate — called on
    /// activation and on online re-validation.
    /// </summary>
    public record ValidateLicenseRequest
    {
        /// <summary>The license key being validated.</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>The client machine's fingerprint (bound on first activation).</summary>
        public string MachineId { get; init; } = "";

        /// <summary>The app requesting validation.</summary>
        public string AppId { get; init; } = "";

        /// <summary>The app version reported for telemetry.</summary>
        public string AppVersion { get; init; } = "1.0.0";
    }
}
