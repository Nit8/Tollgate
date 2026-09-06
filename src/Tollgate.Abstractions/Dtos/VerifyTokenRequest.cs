using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>
    /// Public: request body for POST /api/license/verify-token — server-side
    /// verification of a cached JWT.
    /// </summary>
    public record VerifyTokenRequest
    {
        /// <summary>The cached JWT to verify.</summary>
        public string Token { get; init; } = "";

        /// <summary>The machine fingerprint the token must be bound to.</summary>
        public string MachineId { get; init; } = "";

        /// <summary>The app the token must be bound to.</summary>
        public string AppId { get; init; } = "";
    }
}
