using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    public record VerifyTokenRequest(string Token, string MachineId, string AppId);
}
