# Tollgate.AspNetCore

ASP.NET Core integration for [Tollgate.Licensing](https://www.nuget.org/packages/Tollgate.Licensing).

## Install

```bash
dotnet add package Tollgate.Abstractions
dotnet add package Tollgate.Licensing
dotnet add package Tollgate.AspNetCore
```

## Use

In `Program.cs`:

```csharp
builder.Services.AddControllers(o => o.Filters.Add<RequireFeatureFilter>());
builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
```

Then annotate any controller / action:

```csharp
public class TodoController : Controller
{
    [RequireFeature("export-pdf")]
    public IActionResult Export() => View();

    [RequireTier(LicenseTier.Pro)]
    public IActionResult BulkImport() => View();
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

Override `RequireFeatureFilter` to redirect to a custom upgrade page if you prefer.
