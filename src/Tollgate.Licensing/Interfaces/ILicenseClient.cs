using System;
using System.Collections.Generic;
using System.Text;
using Tollgate.Abstractions;
using Tollgate.Abstractions.Dtos;

namespace Tollgate.Licensing.Interfaces
{
    /// <summary>Interface for the license client (useful for tests / DI).</summary>
    public interface ILicenseClient
    {
        LicenseState Current { get; }
        Task<bool> TryLoadSavedLicenseAsync();
        Task<ValidateLicenseResponse> ActivateKeyAsync(string licenseKey);
        void ClearLicense();
    }

}
