# Tollgate — Drop-in SaaS Licensing for .NET

> *Turn any .NET app into a freemium / tiered / feature-gated product in 5 minutes.*
> *Self-host your own license server. Ship a NuGet package. Done.*

Tollgate is a complete, open-source licensing toolkit that turns any .NET application — Console, WinForms, WPF, ASP.NET Core — into a tiered/feature-gated product. Drop a `[RequireFeature("export-pdf")]` attribute on a method, run your own Tollgate server, and you have SaaS-grade licensing without rewriting your code.

---

## ✨ Features

| Capability                              | Tollgate |
|-----------------------------------------|----------|
| Drop-in `[RequireFeature]` attribute   | ✅       |
| Drop-in `[RequireTier]` attribute       | ✅       |
| ASP.NET Core auto-enforcement (filter)  | ✅       |
| Manual enforcement for Console/WinForms | ✅       |
| Self-hostable license & keygen server   | ✅       |
| Multi-tenant (multiple apps per server) | ✅       |
| Offline grace period (cached JWT)       | ✅       |
| Machine binding (anti-piracy)           | ✅       |
| Cross-platform machine fingerprint      | ✅       |
| Encrypted local cache (DPAPI + AES-GCM) | ✅       |
| Tier + arbitrary feature flags          | ✅       |
| Docker / systemd / nginx deploy recipes | ✅       |
| Admin CLI for key/feature management    | ✅       |
| Swagger UI on the server                | ✅       |
| Auto-scaffolded `tollgate.json` via MSBuild | ✅   |
| MIT license                             | ✅       |

---

## 📦 NuGet packages

| Package | What it's for | Install |
|---------|---------------|---------|
| `Tollgate.Abstractions` | Attributes, enums, DTOs. Reference in **every** project (client + server). | `dotnet add package Tollgate.Abstractions` |
| `Tollgate.Licensing` | The client library — validation, caching, machine fingerprint, `LicenseGate`. | `dotnet add package Tollgate.Licensing` |
| `Tollgate.AspNetCore` | MVC filter that auto-enforces `[RequireFeature]` on controllers/actions. Add only to ASP.NET Core apps. | `dotnet add package Tollgate.AspNetCore` |

The `Tollgate.Server` and `Tollgate.KeyGen` projects are **not NuGet packages** — they are deployable binaries. Clone the repo and `dotnet publish`.

---

## 🏗 Project structure

```
Tollgate/
├── Tollgate.slnx                      # Solution file (new XML format)
├── build.ps1                          # Windows build + pack script
├── build.sh                           # Linux/macOS build + pack script
├── LICENSE.txt                         # MIT
├── README.md                          # ← you are here
│
├── src/
│   ├── Tollgate.Abstractions/          # NuGet: attributes, enums, DTOs, exceptions
│   │   ├── Enums/LicenseTier.cs
│   │   ├── Dtos/                       # one DTO per file
│   │   ├── Attributes.cs
│   │   ├── Exceptions.cs
│   │   ├── LicenseState.cs
│   │   ├── LicenseTiers.cs
│   │   └── README.md
│   │
│   ├── Tollgate.AspNetCore/           # NuGet: RequireFeatureFilter for MVC
│   │   ├── RequireFeatureFilter.cs
│   │   └── README.md
│   │
│   ├── Tollgate.Server/               # Self-hostable ASP.NET Core license server
│   │   ├── Controllers/
│   │   │   ├── LicenseController.cs    # /api/license/*
│   │   │   └── AdminController.cs      # /api/admin/*
│   │   ├── Data/
│   │   │   ├── LicenseDbContext.cs
│   │   │   ├── LicenseKeyEntity.cs
│   │   │   └── AppEntity.cs
│   │   ├── Services/
│   │   │   ├── TokenService.cs         # JWT signing
│   │   │   └── LicenseKeyGenerator.cs  # PRO-XXXX-XXXX-XXXX-XXXX
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   └── Tollgate.Server.http        # VS REST client samples
│   │
│   └── Tollgate.KeyGen/               # Admin CLI for managing keys
│       └── Program.cs
│
├── Tollgate.Licensing/                # NuGet: LicenseClient, LicenseGate, cache, config
│   ├── Interfaces/ILicenseClient.cs
│   ├── LicenseCache/
│   │   ├── LicenseCache.cs             # encrypted local cache
│   │   ├── CachedLicense.cs
│   │   └── CachePayload.cs
│   ├── LicenseClient.cs                # HTTP client + JWT validation
│   ├── LicenseGate.cs                  # static accessor — the simplest API
│   ├── MachineFingerprint.cs            # cross-platform hardware ID
│   ├── TollgateOptions.cs
│   ├── TollgateConfig.cs               # tollgate.json discovery
│   ├── ServiceCollectionExtensions.cs  # AddTollgate() DI helper
│   ├── build/                          # MSBuild auto-scaffold target
│   │   ├── Tollgate.Licensing.props
│   │   └── tollgate.template.json
│   └── README.md
│
└── deploy/
    ├── docker/
    │   ├── Dockerfile
    │   ├── docker-compose.yml
    │   └── .env.example
    ├── systemd/tollgate.service
    ├── nginx/tollgate.conf
    └── README.md                       # deployment guide
```

---

## 🚀 Quick start (5 minutes, end-to-end)

### Step 1 — Run the Tollgate server

Pick **one**:

<details>
<summary><b>Option A: Run from source</b></summary>

```bash
cd Tollgate
dotnet run --project src/Tollgate.Server
```

Server is at `http://localhost:5000`. Swagger UI at `http://localhost:5000/swagger`.
</details>

<details>
<summary><b>Option B: Docker</b></summary>

```bash
cd deploy/docker
cp .env.example .env
nano .env     # set TOLLGATE_JWT_SECRET and TOLLGATE_ADMIN_KEY
docker compose up -d --build
```
</details>

<details>
<summary><b>Option C: systemd + nginx (production)</b></summary>

See [`deploy/README.md`](deploy/README.md) for full instructions.
</details>

### Step 2 — Generate your first license keys

```bash
dotnet run --project src/Tollgate.KeyGen
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
    o.ServerUrl = "http://localhost:5000";
    o.AppId     = "my-todo-app";
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
}
```

For ASP.NET Core, add the filter once:

```csharp
builder.Services.AddTollgate(builder.Configuration.GetSection("Tollgate"));
builder.Services.AddControllersWithViews(o =>
    o.Filters.Add<RequireFeatureFilter>());
```

`[RequireFeature]` / `[RequireTier]` are now enforced automatically on every controller action.

---

## 🧱 Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                          Your .NET app                        │
│   ┌──────────────┐    ┌──────────────────────────────────┐   │
│   │ Your code     │    │  Tollgate.Licensing (NuGet)     │   │
│   │ [RequireFeat] │◄───┤  LicenseGate.EnsureAccessFor()   │   │
│   │ [RequireTier] │    │  LicenseClient (HTTP)            │   │
│   └──────────────┘    │  LicenseCache (DPAPI/AES-GCM)    │   │
│                       │  MachineFingerprint               │   │
│                       └───────────────┬──────────────────┘   │
└───────────────────────────────────────┼──────────────────────┘
                                        │ HTTPS (POST /api/license/validate)
                                        ▼
                       ┌──────────────────────────────────────┐
                       │  Tollgate.Server (your own VPS)       │
                       │  • validates license key              │
                       │  • binds to machine fingerprint       │
                       │  • returns signed JWT + tier +       │
                       │    features                          │
                       │  • SQLite / Postgres / SQL Server    │
                       └──────────────────────────────────────┘
                                        ▲
                                        │ Admin API
                       ┌────────────────┴─────────────────────┐
                       │  Tollgate.KeyGen CLI                 │
                       │  • generate keys                    │
                       │  • revoke / reset machine           │
                       │  • manage features per key          │
                       └──────────────────────────────────────┘
```

### How it works

1. **First activation** — App sends `LicenseKey + MachineId + AppId` to `/api/license/validate`.
2. **Server validates** — checks DB, binds the key to the machine (one key = one machine by default).
3. **Server issues a signed JWT** containing `tier` and `features` claims, valid for 7 days.
4. **Client caches** the JWT encrypted with Windows DPAPI (or AES-GCM on Linux/macOS).
5. **Subsequent launches** — App validates the cached JWT locally (no internet needed).
6. **After grace period** — App silently re-validates online. If the key was revoked, the cache is purged.

---

## 🎯 Developer API

### Attributes

```csharp
[RequireFeature("export-pdf")]                        // gate by feature flag
[RequireFeature("export-pdf", DeniedMessage = "…")]    // custom deny message
[RequireTier(LicenseTier.Pro)]                        // gate by tier (Pro, Enterprise, …)
[RequireTrial]                                         // gate to trial users only
```

### LicenseGate (static accessor — Console / WinForms / WPF)

```csharp
LicenseGate.Configure(o => { o.ServerUrl = "…"; o.AppId = "…"; });
LicenseGate.ConfigureFromConfigFile();                 // auto-discover tollgate.json
await LicenseGate.TryLoadSavedLicenseAsync();          // load from cache
await LicenseGate.ActivateKeyAsync(key);              // online activation
LicenseGate.ClearLicense();                            // sign out

LicenseGate.Current.Tier                               // LicenseTier.Pro
LicenseGate.Current.Features                          // List<string>
LicenseGate.Current.HasFeature("export-pdf")          // bool

LicenseGate.EnsureFeature("export-pdf");              // throws LicenseRequiredException
LicenseGate.EnsureTier(LicenseTier.Pro);              // throws LicenseRequiredException

LicenseGate.EnsureAccessFor(methodInfo);              // reflect & enforce attributes
```

### DI registration (ASP.NET Core)

```csharp
// appsettings.json
{
  "Tollgate": {
    "ServerUrl": "https://license.myapp.com",
    "AppId":     "my-todo-app",
    "AppVersion":"1.0.0"
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

## 📁 Configuration via `tollgate.json` (recommended)

Drop a `tollgate.json` file in your project. Tollgate auto-discovers it on startup so your code stays config-free.

### Schema

```json
{
  "serverUrl": "http://localhost:5000",
  "appId": "my-app",
  "appVersion": "1.0.0",
  "adminKey": "REPLACE_WITH_YOUR_ADMIN_KEY",
  "publicKey": "",
  "sharedSecret": "",
  "cacheFile": "license.dat",
  "offlineGraceDays": 7,
  "allowFreeMode": true
}
```

| Field | Purpose |
|---|---|
| `serverUrl` | Tollgate server URL |
| `appId` | Your app's unique ID |
| `appVersion` | Reported to server for analytics |
| `adminKey` | Only needed by KeyGen CLI; **leave blank for client apps** |
| `publicKey` | RSA PEM (optional — for asymmetric JWT verification) |
| `sharedSecret` | HMAC secret (optional — when not using RSA) |
| `cacheFile` | Cache filename (relative to user app-data dir) |
| `offlineGraceDays` | Days a cached JWT is honored offline |
| `allowFreeMode` | If `true`, app runs even with no license |

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

## 🖥 Server API reference

### Public (no admin key required)

| Method | Endpoint                       | Description |
|--------|--------------------------------|-------------|
| `POST` | `/api/license/validate`        | Validate a key, bind to machine, return JWT + features |
| `POST` | `/api/license/verify-token`    | Validate a cached JWT |
| `GET`  | `/api/license/health`          | Health check |

### Admin (requires `X-Admin-Key` header or `AdminKey` in body)

| Method | Endpoint                          | Description |
|--------|-----------------------------------|-------------|
| `POST` | `/api/admin/generate`             | Generate N keys for an app/tier, with optional features & expiry |
| `POST` | `/api/admin/set-features`         | Update the feature list on an existing key |
| `POST` | `/api/admin/revoke`               | Revoke a key |
| `GET`  | `/api/admin/keys`                 | List keys (filter by app, tier, active) |
| `POST` | `/api/admin/reset-machine`        | Clear machine binding (license transfer) |
| `POST` | `/api/admin/apps/register`        | Register a new app (multi-tenant) |
| `GET`  | `/api/admin/apps`                 | List all registered apps |

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

## 🔧 Configuration reference

### Client (`TollgateOptions`)

| Property            | Default            | Description |
|---------------------|--------------------|-------------|
| `ServerUrl`         | `http://localhost:5000` | Tollgate server URL |
| `AppId`             | `"default"`        | Your application ID (must match a registered app on the server) |
| `AppVersion`        | `"1.0.0"`          | Reported to the server for analytics |
| `PublicKey`         | `""`               | RSA public key (PEM) for asymmetric JWT verification. Empty = use shared secret. |
| `SharedSecret`      | `""`               | HMAC secret for symmetric JWT verification (matches `Jwt:Secret` on server) |
| `CacheFile`         | `"license.dat"`    | Encrypted cache filename |
| `CacheDirectory`    | (auto)             | Override cache dir (default = `%LOCALAPPDATA%/Tollgate/<AppId>/`) |
| `HttpTimeout`       | `00:00:10`         | HTTP timeout for server calls |
| `OfflineGraceDays`  | `7`                | Days a cached token is honored offline |
| `AllowFreeMode`      | `true`             | If `false`, throws when no license is configured |

### Server (`appsettings.json`)

| Section        | Key                  | Description |
|----------------|----------------------|-------------|
| `Jwt:Secret`   | (32+ chars random)  | HMAC signing secret. **Change in production!** |
| `Jwt:Issuer`   | `TollgateServer`    | JWT `iss` claim |
| `Jwt:Audience` | `TollgateClient`     | JWT `aud` claim |
| `Jwt:TokenLifetimeDays` | `7` | How long the client can go without re-validating |
| `Admin:Key`    | (strong password)   | Admin password for `/api/admin/*`. **Change in production!** |
| `Urls`         | `http://0.0.0.0:5000` | Kestrel bind URL |

---

## 🔐 Security checklist for production

- [ ] **Change `Jwt:Secret`** to a 64+ char random string (`openssl rand -base64 48`).
- [ ] **Change `Admin:Key`** to a strong, unique password.
- [ ] **Use HTTPS** — deploy behind nginx with Let's Encrypt (see `deploy/nginx/tollgate.conf`).
- [ ] **Restrict CORS** — change `AllowAnyOrigin` to your app's domain.
- [ ] **Restrict Swagger** to internal networks (uncomment in `deploy/nginx/tollgate.conf`).
- [ ] **Switch to RSA signing** — no shared secret on the client.
- [ ] **Back up the SQLite DB** (or switch to Postgres with backups).
- [ ] **Rate-limit** `/api/license/validate` (built-in limiter exists, tune as needed).
- [ ] **Add `tollgate.json` to `.gitignore`** — it contains secrets.

---

## 🛠 Build & publish

### Build everything + pack NuGet packages

```bash
# Linux/macOS
./build.sh

# Windows PowerShell
.\build.ps1
```

Produces `artifacts/nuget/` with three `.nupkg` files.

### Publish the server for deployment

```bash
dotnet publish src/Tollgate.Server -c Release -o ./publish/server
```

---

## 📄 License

MIT — see [LICENSE.txt](LICENSE.txt).

Replace `your-org` placeholders in the `.csproj` files before publishing to nuget.org.
