using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Tollgate.Abstractions;
using Tollgate.Abstractions.Enums;
using Tollgate.Licensing;
using Tollgate.Licensing.Interfaces;

namespace Tollgate.AspNetCore
{
    // ─────────────────────────────────────────────────────────────
    //  ASP.NET CORE ACTION FILTER
    //
    //  Automatically enforces [RequireFeature] / [RequireTier] /
    //  [RequireTrial] on controllers and actions.
    //
    //  Register globally via:
    //      services.AddControllers(o => o.Filters.Add<RequireFeatureFilter>());
    //  (The Tollgate.AspNetCore package exposes this filter — register it once.)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ASP.NET Core filter that gates an action by checking the
    /// <see cref="RequireFeatureAttribute"/>, <see cref="RequireTierAttribute"/>
    /// and <see cref="RequireTrialAttribute"/> declared on the controller or
    /// action.
    /// </summary>
    public class RequireFeatureFilter : IAsyncAuthorizationFilter
    {
        private readonly ILicenseClient? _client;

        /// <summary>
        /// DI constructor. If <see cref="ILicenseClient"/> is registered in DI,
        /// it will be used; otherwise the filter falls back to the static
        /// <see cref="LicenseGate.Current"/>.
        /// </summary>
        public RequireFeatureFilter(ILicenseClient? client = null) => _client = client;

        /// <inheritdoc />
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // ActionDescriptor does not expose EndpointMetadata. Read the attributes
            // from the controller and action reflected by MVC instead.
            var attrs = context.ActionDescriptor is ControllerActionDescriptor action
                ? action.ControllerTypeInfo
                    .GetCustomAttributes(typeof(Attribute), inherit: true)
                    .Cast<Attribute>()
                    .Concat(action.MethodInfo.GetCustomAttributes(typeof(Attribute), inherit: true).Cast<Attribute>())
                    .ToList()
                : new List<Attribute>();

            // Prefer the DI-resolved client; fall back to the static gate.
            var license = _client?.Current ?? LicenseGate.Current;

            // If neither is initialized, treat as no license (deny by default).
            var tier = license?.Tier ?? LicenseTier.None;
            var features = license?.Features ?? Array.Empty<string>();

            // Check tier first (cheapest)
            foreach (var attr in attrs.OfType<RequireTierAttribute>())
            {
                if ((int)tier < (int)attr.Tier)
                {
                    context.Result = Deny(attr.DeniedMessage ??
                        $"This action requires the {attr.Tier} tier.", attr.Tier);
                    return Task.CompletedTask;
                }
            }

            // Then features
            foreach (var attr in attrs.OfType<RequireFeatureAttribute>())
            {
                if (!features.Contains(attr.Feature, StringComparer.OrdinalIgnoreCase))
                {
                    context.Result = Deny(attr.DeniedMessage ??
                        $"This action requires the '{attr.Feature}' feature.", attr.Feature);
                    return Task.CompletedTask;
                }
            }

            // Then trial-only members: the license must be a valid trial
            // (valid key with tier None, e.g. TRIAL-XXXX-...).
            foreach (var attr in attrs.OfType<RequireTrialAttribute>())
            {
                if (license is null || !license.IsValid || tier != LicenseTier.None)
                {
                    context.Result = Deny(attr.DeniedMessage ??
                        "This action is only available during the trial period.", "trial");
                    return Task.CompletedTask;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Build the deny result. Default is 402 Payment Required with a JSON body.
        /// Inherit from <see cref="RequireFeatureFilter"/> and override to redirect
        /// to a custom upgrade page instead.
        /// </summary>
        protected virtual IActionResult Deny(string message, object required) =>
            new ObjectResult(new
            {
                error = "license_required",
                message,
                required,
                currentTier = (_client?.Current ?? LicenseGate.Current).Tier.ToString(),
                upgradeUrl = "/license/upgrade"
            })
            { StatusCode = 402 };
    }
}
