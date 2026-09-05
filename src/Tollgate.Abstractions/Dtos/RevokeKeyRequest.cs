using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    public record RevokeKeyRequest(string LicenseKey, string AdminKey);
}
