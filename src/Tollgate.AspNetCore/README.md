# Tollgate.AspNetCore

ASP.NET Core integration for [Tollgate.Licensing](https://www.nuget.org/packages/Tollgate.Licensing).

## Install

```bash
dotnet add package Tollgate.Abstractions
dotnet add package Tollgate.Licensing
dotnet add package Tollgate.AspNetCore
```

Targets net8.0 and net10.0.

## Use

In `Program.cs`:

```csharp
builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
builder.Services.AddControllers(o => o.Filters.Add<RequireFeatureFilter>());
```

Then annotate any controller / action:

```csharp
public class TodoController : Controller
{
    [RequireFeature("export-pdf")]
    public IActionResult Export() => View();

    [RequireTier(LicenseTier.Pro)]
    public IActionResult BulkImport() => View();

    [RequireTrial]
    public IActionResult Preview() => View();   // valid trial keys only
}
```

Unauthorized callers get a `402 Payment Required` response with a JSON body describing what's missing:

```json
{
  "error": "license_required",
  "message": "This action requires the 'export-pdf' feature.",
  "required": "export-pdf",
  "currentTier": "Basic",
  "upgradeUrl": "/license/upgrade"
}
```

Override `RequireFeatureFilter` and its `Deny(...)` method to redirect to a custom upgrade page if you prefer.
