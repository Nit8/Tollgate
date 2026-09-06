using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Public: request body for POST /api/license/deactivate.
    /// Lets an end user release the machine binding of their own key
    /// (no admin key required — the current machine must match).
    /// </summary>
    public record DeactivateLicenseRequest
    {
        /// <summary>The license key to deactivate.</summary>
        public string LicenseKey { get; init; } = "";

        /// <summary>The machine fingerprint the key is currently bound to.</summary>
        public string MachineId { get; init; } = "";

        /// <summary>The app the key belongs to.</summary>
        public string AppId { get; init; } = "";
    }
}
