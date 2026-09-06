# Tollgate.Licensing

The client library for [Tollgate](https://github.com/your-org/tollgate) — drop-in SaaS licensing for any .NET app.

## Install

```bash
dotnet add package Tollgate.Abstractions
dotnet add package Tollgate.Licensing
```

## Quick start (Console / WinForms / WPF)

```csharp
using Tollgate.Licensing;

// 1. Configure once at startup
LicenseGate.Configure(options =>
{
    options.ServerUrl = "https://license.myapp.com";
    options.AppId     = "my-todo-app";
    options.CacheFile  = "license.dat";  // optional, default = license.dat
});

// 2. Try to load existing license (offline-first)
var loaded = await LicenseGate.TryLoadSavedLicenseAsync();
if (!loaded)
{
    Console.Write("Enter license key: ");
    var key = Console.ReadLine()!;
    var result = await LicenseGate.ActivateKeyAsync(key);
    Console.WriteLine(result.IsValid ? "Activated!" : result.Message);
}

// 3. Gate any code path
LicenseGate.EnsureFeature("export-pdf");   // throws LicenseRequiredException
LicenseGate.EnsureTier(LicenseTier.Pro);    // throws LicenseRequiredException
```

## Quick start (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));

// Controllers
[RequireFeature("export-pdf")]
public IActionResult Export() => View();
```

## Auto-scaffolded config file

When you `dotnet add package Tollgate.Licensing` and build, an MSBuild target
inside the package auto-creates `tollgate.json` in your project directory. Just
edit `appId` and (optionally) `adminKey`. The client auto-discovers it at runtime.

See the [main README](https://github.com/your-org/tollgate) for the full guide.
