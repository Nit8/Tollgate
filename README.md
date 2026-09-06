# Tollgate — Drop-in SaaS Licensing for .NET

> *Turn any .NET app into a freemium / tiered / feature-gated product in 5 minutes.*
> *Self-host your own license server. Ship a NuGet package. Done.*

Tollgate is a complete, open-source licensing toolkit that turns any .NET application — Console, WinForms, WPF, ASP.NET Core — into a tiered/feature-gated product. Drop a `[RequireFeature("export-pdf")]` attribute on a method, run your own Tollgate server, and you have SaaS-grade licensing without rewriting your code.

---

## Features

| Capability                              | Tollgate |
|-----------------------------------------|----------|
| Drop-in `[RequireFeature]` attribute   | ✅ |
| Drop-in `[RequireTier]` attribute       | ✅ |
| Drop-in `[RequireTrial]` attribute      | ✅ |
| ASP.NET Core auto-enforcement (filter)  | ✅ |
| Manual enforcement for Console/WinForms | ✅ |
| Self-hostable license & keygen server   | ✅ |
| Multi-tenant (multiple apps per server) | ✅ |
| **Cryptographically verified** offline cache (JWT signature, issuer, audience, expiry, machine + app binding) | ✅ |
| Offline grace period (configurable)     | ✅ |
| Machine binding (anti-piracy)           | ✅ |
| End-user deactivation (license transfer)| ✅ |
| Cross-platform machine fingerprint      | ✅ |
| Encrypted local cache (DPAPI + AES-GCM) | ✅ |
| Tier + arbitrary feature flags          | ✅ |
| Server telemetry: last-seen + app version per key | ✅ |
| Docker / systemd / nginx deploy recipes | ✅ |
| Admin CLI for key/feature management (also packaged as a .NET global tool) | ✅ |
| Swagger UI on the server                | ✅ |
| Auto-scaffolded `tollgate.json` via MSBuild | ✅ |
| MIT license                             | ✅ |

---

## NuGet packages

| Package | What it's for | Install |
|---------|---------------|---------|
| `Tollgate.Abstractions` | Attributes, enums, DTOs. Reference in **every** project (client + server). Targets **netstandard2.0** + net10.0 — works on every .NET of the last decade. | `dotnet add package Tollgate.Abstractions` |
| `Tollgate.Licensing` | The client library — validation, verified cache, machine fingerprint, `LicenseGate`. Targets net8.0 / net10.0 / net10.0-windows. | `dotnet add package Tollgate.Licensing` |
| `Tollgate.AspNetCore` | MVC filter that auto-enforces `[RequireFeature]` / `[RequireTier]` / `[RequireTrial]` on controllers/actions. | `dotnet add package Tollgate.AspNetCore` |
| `Tollgate.KeyGen` *(optional)* | The admin CLI as a .NET global tool. | `dotnet tool install -g Tollgate.KeyGen` |

The `Tollgate.Server` project is **not a NuGet package** — it is a deployable binary. Clone the repo and `dotnet publish`, or use the Docker image.

---

## Project structure

```
Tollgate/
├── Tollgate.slnx                       # Solution file (new XML format — needs VS 17.13+ / .NET SDK 8+)
├── Directory.Build.props               # Shared NuGet metadata (version, authors, license, icon)
├── Directory.Build.targets             # Shared items (icon + SourceLink for packable projects)
├── Directory.Packages.props            # Central Package Management (all versions, one place)
├── icon.png                            # Package icon (embedded in every package)
├── build.ps1 / build.sh                # Build + test + pack scripts (verify package output)
├── LICENSE.txt                         # MIT
├── CHANGELOG.md / CONTRIBUTING.md / SECURITY.md
├── README.md                           # ← you are here
│
├── src/
│   ├── Tollgate.Abstractions/          # NuGet: attributes, enums, DTOs, exceptions
│   │   ├── Compat/IsExternalInit.cs    # netstandard2.0 record/init shim
│   │   ├── Dtos/                       # one DTO per file
│   │   ├── Attributes.cs / Exceptions.cs
│   │   ├── LicenseState.cs / LicenseTiers.cs
│   │   └── README.md
│   │
│   ├── Tollgate.Licensing/             # NuGet: LicenseClient, LicenseGate, cache, config
│   │   ├── Interfaces/ILicenseClient.cs
│   │   ├── LicenseCache/
│   │   │   ├── LicenseStore.cs         # encrypted local cache (DPAPI / AES-GCM)
│   │   │   ├── CachedLicense.cs
│   │   │   └── CachePayload.cs
│   │   ├── LicenseClient.cs            # HTTP + full JWT verification
│   │   ├── LicenseGate.cs              # static accessor — the simplest API
│   │   ├── MachineFingerprint.cs       # cross-platform hardware ID
│   │   ├── TollgateOptions.cs / TollgateConfig.cs
│   │   ├── ServiceCollectionExtensions.cs   # AddTollgate() DI helper
│   │   ├── build/                      # MSBuild auto-scaffold target
│   │   └── README.md
│   │
│   ├── Tollgate.AspNetCore/            # NuGet: RequireFeatureFilter for MVC
│   │   ├── RequireFeatureFilter.cs
│   │   └── README.md
│   │
│   ├── Tollgate.Server/                # Self-hostable ASP.NET Core license server
│   │   ├── Controllers/ (LicenseController, AdminController)
│   │   ├── Data/ (LicenseDbContext, entities, AdminAuditEntity)
│   │   ├── Services/ (TokenService, LicenseKeyGenerator)
│   │   └── Program.cs
│   │
│   └── Tollgate.KeyGen/                # Admin CLI (packable as a global tool)
│       ├── Program.cs
│       └── README.md
│
├── tests/Tollgate.Core.Tests/          # xUnit tests: tier logic, cache, JWT
│                                       # verification (forgery/expiry/machine),
│                                       # grace semantics, config discovery
│
├── samples/Tollgate.Samples.ConsoleApp/
├── deploy/ (docker, systemd, nginx)
└── .github/workflows/ci.yml            # build + test + pack on every push
```

---

## Quick start (5 minutes, end-to-end)

### Step 1 — Run the Tollgate server

Pick **one**:

<details>
<summary><b>Option A: Run from source</b></summary>

```bash
cd Tollgate
dotnet run --project src/Tollgate.Server
```

Server is at `http://localhost:7431`. Swagger UI at `http://localhost:7431/swagger` (Development only).
</details>

<details>
<summary><b>Option B: Docker</b></summary>

```bash
cd deploy/docker
cp .env.example .env
nano .env     # set TOLLGATE_JWT_SECRET, TOLLGATE_ADMIN_KEY, TOLLGATE_CORS_ORIGINS
docker compose up -d --build
```
</details>

<details>
<summary><b>Option C: systemd + nginx (production)</b></summary>

See [`deploy/README.md`](deploy/README.md) for full instructions.
</details>

### Step 2 — Generate your first license keys

```bash
# interactive
dotnet run --project src/Tollgate.KeyGen

# or one-shot (CI-friendly)
dotnet run --project src/Tollgate.KeyGen -- generate \
    --app my-todo-app --tier Pro --features export-pdf,ai-assist --count 1
```

The CLI auto-discovers `tollgate.json` if it exists, or prompts you for server URL + admin key. Then:

```
> Generate license keys
  App ID: my-todo-app
  Tier:   Pro
  Features: export-pdf, ai-assist

✓ PRO-A3F2-9B1C-E7D4-2F8A
```

### Step 3 — Install Tollgate in your app

```bash
cd /path/to/your-app
dotnet add package Tollgate.Abstractions
dotnet add package Tollgate.Licensing
# If ASP.NET Core:
dotnet add package Tollgate.AspNetCore
```

> On first `dotnet build`, Tollgate auto-creates a `tollgate.json` in your project directory. Edit `appId` and you're done.

### Step 4 — Configure & gate features

```csharp
using Tollgate.Licensing;

// Auto-discover tollgate.json (recommended)
LicenseGate.ConfigureFromConfigFile();

// OR configure programmatically:
LicenseGate.Configure(o =>
{
    o.ServerUrl = "http://localhost:7431";
    o.AppId     = "my-todo-app";

    // REQUIRED for offline caching: set one of these so cached tokens
    // can be cryptographically verified. Without a key, every launch
    // re-validates online (fail-closed by design).
    //   o.PublicKey   = "-----BEGIN PUBLIC KEY-----...";  // recommended
    //   o.SharedSecret = "<Jwt:Secret from the server>";
});

var loaded = await LicenseGate.TryLoadSavedLicenseAsync();
if (!loaded)
{
    Console.Write("License key: ");
    var key = Console.ReadLine()!;
    await LicenseGate.ActivateKeyAsync(key);
}

// Gate any method:
public class TodoService
{
    [RequireFeature("export-pdf")]
    public byte[] ExportPdf() { /* ... */ }

    [RequireTier(LicenseTier.Pro)]
    public void BulkImport() { /* ... */ }

    [RequireTrial]
    public void PreviewFeature() { /* trial users only */ }
}
```

For ASP.NET Core, add the filter once:

```csharp
builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
builder.Services.AddControllersWithViews(o =>
    o.Filters.Add<RequireFeatureFilter>());
```

`[RequireFeature]` / `[RequireTier]` / `[RequireTrial]` are now enforced automatically on every controller action.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                          Your .NET app                        │
│   ┌──────────────┐    ┌──────────────────────────────────┐   │
│   │ Your code     │    │  Tollgate.Licensing (NuGet)     │   │
│   │ [RequireFeat] │◄───┤  LicenseGate.EnsureAccessFor()   │   │
│   │ [RequireTier] │    │  LicenseClient (HTTP)            │   │
│   └──────────────┘    │  LicenseStore (DPAPI/AES-GCM)    │   │
│                       │  JWT signature verification       │   │
│                       │  MachineFingerprint               │   │
│                       └───────────────┬──────────────────┘   │
└───────────────────────────────────────┼──────────────────────┘
                                        │ HTTPS (POST /api/license/validate)
                                        ▼
                       ┌────────────────────────────────────┐
                       │  Tollgate.Server (your own VPS)       │
                       │  • validates license key              │
                       │  • binds to machine fingerprint       │
                       │  • returns signed JWT + tier +       │
                       │    features                          │
                       │  • SQLite (configurable path)         │
                       └────────────────────────────────────┘
                                        ▲
                                        │ Admin API (X-Admin-Key header)
                       ┌────────────────┴─────────────────────┐
                       │  Tollgate.KeyGen CLI                 │
                       │  • generate / revoke / reset         │
                       │  • manage features per key          │
                       └────────────────────────────────────┘
```

### How it works

1. **First activation** — App sends `LicenseKey + MachineId + AppId` to `/api/license/validate`.
2. **Server validates** — checks DB, binds the key to the machine (one key = one machine by default).
3. **Server issues a signed JWT** containing `tier` and `features` claims, valid for 7 days.
4. **Client verifies + caches** — the token signature, issuer, audience, machine and app binding are **all verified before the response is trusted**; then the JWT is cached encrypted with Windows DPAPI (or AES-GCM on Linux/macOS).
5. **Subsequent launches** — within the offline grace window, the cached JWT is re-verified cryptographically and honored with **no server call**. Without a configured `PublicKey`/`SharedSecret`, the client fails closed and re-validates online instead.
6. **After the grace period** — the client re-validates online. A revoked key purges the cache; an unreachable server keeps it for retry but does not honor a stale token.

### Threat model (honest version)

Any .NET licensing library can be defeated by a determined attacker with a decompiler — attributes and `Ensure*` calls can be patched out of a compiled binary. Tollgate's goals are: raising the cost of casual piracy, supporting honest customers gracefully (offline grace, self-service deactivation), and keeping server-side truth for anything that matters commercially. Cached tokens are **verified, not trusted** — forging one requires the server's private key (RSA mode) or shared secret.

---

## Developer API

### Attributes

```csharp
[RequireFeature("export-pdf")]                        // gate by feature flag
[RequireFeature("export-pdf", DeniedMessage = "…")]    // custom deny message
[RequireTier(LicenseTier.Pro)]                        // gate by tier (Basic / Pro / Enterprise)
[RequireTrial]                                        // trial-only (valid None-tier key)
```

### LicenseGate (static accessor — Console / WinForms / WPF)

```csharp
LicenseGate.Configure(o => { o.ServerUrl = "…"; o.AppId = "…"; });
LicenseGate.ConfigureFromConfigFile();                 // auto-discover tollgate.json
await LicenseGate.TryLoadSavedLicenseAsync();          // verified cache or online
await LicenseGate.ActivateKeyAsync(key);              // online activation
await LicenseGate.DeactivateAsync();                  // release machine binding (transfer)
LicenseGate.ClearLicense();                            // local sign-out

LicenseGate.Current.Tier                               // LicenseTier.Pro
LicenseGate.Current.Features                          // List<string>
LicenseGate.Current.HasFeature("export-pdf")          // bool

LicenseGate.EnsureFeature("export-pdf");              // throws LicenseRequiredException
LicenseGate.EnsureTier(LicenseTier.Pro);              // throws LicenseRequiredException
LicenseGate.EnsureTrial();                             // throws unless valid trial

LicenseGate.EnsureAccessFor(methodInfo);              // reflect & enforce attributes
```

> **Strict mode:** with `AllowFreeMode = false`, gate checks throw `LicenseNotConfiguredException` (not `LicenseRequiredException`) when no license is active — for apps that must not run unlicensed at all.

### DI registration (ASP.NET Core)

```csharp
// appsettings.json
{
  "Tollgate": {
    "ServerUrl": "https://license.myapp.com",
    "AppId":     "my-todo-app",
    "AppVersion":"1.0.0",
    "PublicKey": "-----BEGIN PUBLIC KEY-----…"
  }
}

// Program.cs
builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
builder.Services.AddControllersWithViews(o =>
    o.Filters.Add<RequireFeatureFilter>());
```

### Catching `LicenseRequiredException` in UI code

```csharp
try
{
    LicenseGate.EnsureFeature("export-pdf");
    ExportPdf();
}
catch (LicenseRequiredException ex)
{
    MessageBox.Show(ex.Message);   // or open your upgrade dialog
}
```

---

## Configuration via `tollgate.json` (recommended)

Drop a `tollgate.json` file in your project. Tollgate auto-discovers it on startup so your code stays config-free.

### Schema

```json
{
  "serverUrl": "http://localhost:7431",
  "appId": "my-app",
  "appVersion": "1.0.0",

  "adminKey": "",

  "publicKey": "",
  "sharedSecret": "",
  "issuer": "TollgateServer",
  "audience": "TollgateClient",

  "cacheDirectory": "",
  "cacheFile": "license.dat",
  "httpTimeoutSeconds": 10,
  "offlineGraceDays": 7,
  "allowFreeMode": true
}
```

| Field | Purpose |
|---|---|
| `serverUrl` | Tollgate server URL |
| `appId` | Your app's unique ID |
| `appVersion` | Reported to the server (stored as per-key telemetry) |
| `adminKey` | Only needed by KeyGen CLI; **leave blank for client apps** |
| `publicKey` | RSA public key (PEM/XML/base64-DER). **Set this for verified offline caching.** |
| `sharedSecret` | HMAC secret matching `Jwt:Secret` on the server (alternative to `publicKey`) |
| `issuer` / `audience` | Must match `Jwt:Issuer` / `Jwt:Audience` on the server |
| `cacheDirectory` | Override cache dir (default = per-platform user app-data) |
| `cacheFile` | Cache filename |
| `httpTimeoutSeconds` | HTTP timeout for server calls |
| `offlineGraceDays` | Days a verified cached token is honored offline (0 = always online) |
| `allowFreeMode` | `false` → gate checks throw `LicenseNotConfiguredException` when unlicensed |

### Discovery order (first match wins)

| # | Location | When to use |
|---|---|---|
| 1 | `$TOLLGATE_CONFIG` env var path | Ad-hoc override |
| 2 | `./tollgate.json` (next to app binary) | **Per-project** (recommended) |
| 3 | `~/.tollgate/tollgate.json` (Win) or `~/.config/tollgate/tollgate.json` (Linux) | Per-user, shared |

### Auto-scaffolding — zero setup

When a project references the `Tollgate.Licensing` NuGet package, an MSBuild target runs on the **first build** and auto-creates `tollgate.json` in the project directory:

```
$ dotnet add package Tollgate.Licensing
$ dotnet build
═══════════════════════════════════════════════════════════════
  Tollgate: created tollgate.json in your project directory.
  Edit it to set your appId and (optionally) adminKey.
  Add tollgate.json to .gitignore if it contains secrets!
═══════════════════════════════════════════════════════════════
```

### Loading config from code

```csharp
// Option A — auto-discover (recommended)
var loadedFrom = LicenseGate.ConfigureFromConfigFile();

// Option B — fall back to inline if no config file exists
if (!LicenseGate.TryConfigureFromConfigFile())
{
    LicenseGate.Configure(o => { o.ServerUrl = "..."; o.AppId = "..."; });
}
```

### Creating a config file with the KeyGen CLI

```bash
$ dotnet run --project src/Tollgate.KeyGen -- init
# Prompts for server URL, admin key, default app ID, etc.
```

---

## Server API reference

### Public (no admin key required)

| Method | Endpoint                       | Description |
|--------|--------------------------------|-------------|
| `POST` | `/api/license/validate`        | Validate a key, bind to machine, return JWT + features; records last-seen telemetry |
| `POST` | `/api/license/verify-token`    | Validate a cached JWT server-side |
| `POST` | `/api/license/deactivate`      | Release machine binding (self-service license transfer, from the bound machine) |
| `GET`  | `/api/license/health`          | Health check (used by Docker) |

### Admin (requires `X-Admin-Key` header — credentials never travel in bodies)

| Method | Endpoint                          | Description |
|--------|-----------------------------------|-------------|
| `POST` | `/api/admin/generate`             | Generate N keys for an app/tier, with optional features & expiry |
| `POST` | `/api/admin/set-features`         | Replace the feature list on an existing key |
| `POST` | `/api/admin/revoke`               | Revoke a key |
| `POST` | `/api/admin/reset-machine`        | Clear machine binding (admin-side transfer) |
| `POST` | `/api/admin/apps/register`        | Register a new app (multi-tenant) |
| `GET`  | `/api/admin/keys`                 | List keys (filter: app/tier/active; paginated: `page`, `pageSize`) |
| `GET`  | `/api/admin/apps`                 | List all registered apps |

All admin operations are rate-limited (60 req/min/IP) and written to a persistent audit table.

### JWT claim shape

```
{
  "lic":  "PRO-A3F2-9B1C-E7D4-2F8A",
  "app":  "my-todo-app",
  "tier": "Pro",
  "mid":  "A1B2C3D4E5F6A1B2",                // machine fingerprint
  "feat": "export-pdf,ai-assist,bulk-import"  // comma-joined features
}
```

---

## Configuration reference

### Client (`TollgateOptions`)

| Property            | Default            | Description |
|---------------------|--------------------|-------------|
| `ServerUrl`         | `http://localhost:7431` | Tollgate server URL |
| `AppId`             | `"default"`        | Your application ID (must match a registered app on the server) |
| `AppVersion`        | `"1.0.0"`          | Reported to the server (stored per key) |
| `PublicKey`         | `""`               | RSA public key (PEM/XML/base64) for asymmetric JWT verification. **Required for offline caching (recommended).** |
| `SharedSecret`      | `""`               | HMAC secret matching `Jwt:Secret` on the server (when not using RSA) |
| `Issuer`            | `"TollgateServer"` | Expected JWT issuer — must match the server |
| `Audience`          | `"TollgateClient"` | Expected JWT audience — must match the server |
| `CacheFile`         | `"license.dat"`    | Encrypted cache filename |
| `CacheDirectory`    | (auto)             | Override cache dir (default = `%LOCALAPPDATA%/Tollgate/<AppId>/`) |
| `HttpTimeout`       | `00:00:10`         | HTTP timeout for server calls |
| `OfflineGraceDays`  | `7`                | Days a **verified** cached token is honored offline (0 = always online) |
| `AllowFreeMode`     | `true`             | If `false`, gate checks throw `LicenseNotConfiguredException` when unlicensed |

### Server (`appsettings.json`)

| Section        | Key                  | Description |
|----------------|----------------------|-------------|
| `Jwt:Secret`   | (32+ chars random)  | HMAC signing secret. **Change in production!** |
| `Jwt:PrivateKey` | (RSA PEM)          | Optional — switch to RSA signing; the client then needs only the public key |
| `Jwt:Issuer` / `Jwt:Audience` | `TollgateServer` / `TollgateClient` | Token issuer/audience |
| `Jwt:TokenLifetimeDays` | `7` | Hard cryptographic limit on offline caching |
| `Admin:Key`    | (strong password)   | Admin password for `/api/admin/*` (header). **Change in production!** |
| `Apps:AllowAutoRegister` | `true` | `false` = AppIds must be registered before key generation |
| `Cors:AllowedOrigins` | (empty) | Comma-separated allow-list — **required in Production** (server refuses to start without it) |
| `Data:Path`    | (auto)               | SQLite file location; auto-detects `/data` (Docker volume) |
| `Urls`         | `http://0.0.0.0:7431` | Kestrel bind URL |

---

## Security checklist for production

- [x] **Client verifies JWT signatures** — offline cache is cryptographically verified (fail-closed without a key)
- [x] **Rate limiting applied** to all endpoints (60/min/IP, 429 responses)
- [x] **CORS locked down** — explicit origin allow-list required in Production
- [x] **Admin auth via header only** — `X-Admin-Key`, compared in constant time
- [x] **Startup guard** — the server refuses to start in Production with placeholder secrets
- [x] **Admin audit trail** — every admin operation persisted to the database
- [ ] **Change `Jwt:Secret`** to a 64+ char random string (`openssl rand -base64 48`) — or switch to `Jwt:PrivateKey` (RSA)
- [ ] **Change `Admin:Key`** to a strong, unique password
- [ ] **Use HTTPS** — deploy behind nginx with Let's Encrypt (see `deploy/nginx/tollgate.conf`)
- [ ] **Configure `Cors:AllowedOrigins`** to your app's domains
- [ ] **Set `PublicKey` on clients** — RSA mode: no secret ships with the client at all
- [ ] **Back up the SQLite DB** (or switch to Postgres with backups) — it's in `/data` (Docker) or `Data:Path`
- [ ] **Add `tollgate.json` to `.gitignore`** — it contains secrets

---

## Build, test & publish

### Build everything + run tests + pack NuGet packages

```bash
# Linux/macOS
./build.sh

# Windows PowerShell
.\build.ps1
```

Both scripts restore, build, run the test suite, pack the three packages into `artifacts/nuget/`, and **verify the expected .nupkg files exist** before exiting successfully. Set `PACK_KEYGEN=1` to also pack the KeyGen CLI as a global tool.

### Publish the server for deployment

```bash
dotnet publish src/Tollgate.Server -c Release -o ./publish/server
```

### Publishing to nuget.org (summary)

1. Fill in the real repository URL / authors in **`Directory.Build.props`** (marked with `TODO`).
2. `./build.sh` → produces `artifacts/nuget/*.nupkg` + `.snupkg` (symbols).
3. Create a nuget.org account (confirm email, enable 2FA) → API Keys → create a key scoped to `Tollgate.*`, 365-day expiry.
4. Push in dependency order:

```bash
dotnet nuget push artifacts/nuget/Tollgate.Abstractions.1.0.0.nupkg \
    --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
dotnet nuget push artifacts/nuget/Tollgate.Licensing.1.0.0.nupkg \
    --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
dotnet nuget push artifacts/nuget/Tollgate.AspNetCore.1.0.0.nupkg \
    --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

Validation takes 15–60 minutes. **Versions are immutable** — a broken upload cannot be re-pushed, so smoke-test the packages locally first. Consider publishing `1.0.0-rc1` before `1.0.0` and request the `Tollgate` prefix reservation once your account is verified.

---

## License

MIT — see [LICENSE.txt](LICENSE.txt).
