# Tollgate.Licensing

The client library for [Tollgate](https://github.com/your-org/tollgate) — drop-in SaaS licensing for any .NET app.

## Install

```bash
dotnet add package Tollgate.Abstractions
dotnet add package Tollgate.Licensing
```

Targets net8.0, net10.0 and net10.0-windows (the Windows target adds WMI fingerprinting and DPAPI cache encryption).

## Quick start (Console / WinForms / WPF)

```csharp
using Tollgate.Licensing;

// 1. Configure once at startup
LicenseGate.Configure(options =>
{
    options.ServerUrl = "https://license.myapp.com";
    options.AppId     = "my-todo-app";

    // REQUIRED for offline caching: one of these lets the client
    // cryptographically verify cached JWTs (fail-closed without one).
    options.PublicKey = "-----BEGIN PUBLIC KEY-----...";   // recommended
    // options.SharedSecret = "<Jwt:Secret from the server>";
});

// 2. Try to load existing license (verified cache first, then online)
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
```

See `Tollgate.AspNetCore` for the automatic MVC filter.

## Security model

Cached tokens are **verified, never trusted**:

- JWT **signature** (RSA public key or HMAC shared secret from options)
- **Issuer** and **audience** claims
- **Expiry** (with 5-minute clock skew)
- **Machine binding** (`mid` claim must match this machine's fingerprint)
- **App binding** (`app` claim must match your `AppId`)

Without a configured key, offline validation is disabled and every launch re-validates online (fail closed). When the grace period expires, an unreachable server does *not* re-honor a stale token. Tokens returned by the server during activation are also verified before being trusted — a man-in-the-middle cannot substitute its own response on plain HTTP.

## Auto-scaffolded config file

When you `dotnet add package Tollgate.Licensing` and build, an MSBuild target
inside the package auto-creates `tollgate.json` in your project directory. Just
edit `appId` and (optionally) `publicKey`. The client auto-discovers it at runtime.

See the [main README](https://github.com/your-org/tollgate) for the full guide.
