# Changelog

All notable changes to the Tollgate project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Cryptographic client-side JWT verification** (`LicenseClient`): cached tokens are now validated for signature (RSA public key or shared secret), issuer, audience, lifetime, machine binding and app binding. Without a configured key the client fails closed (always re-validates online). Tokens returned during online activation are verified before being trusted.
- `Issuer` / `Audience` options on `TollgateOptions`, `TollgateConfig` and the `tollgate.json` template.
- **Offline grace enforcement**: `OfflineGraceDays` is now honored (0 = online-only). Beyond the grace window an unreachable server no longer re-honors a stale token.
- **`AllowFreeMode` enforcement**: with `AllowFreeMode=false`, gate checks throw `LicenseNotConfiguredException` instead of a feature denial.
- **`[RequireTrial]` enforcement** in `LicenseGate.EnsureAccessFor(...)`, the MVC filter, and the new `LicenseGate.EnsureTrial()`.
- **`DeactivateAsync`** on `LicenseClient` / `ILicenseClient` / `LicenseGate` + public `POST /api/license/deactivate` endpoint — end users can move licenses between machines without admin help.
- **KeyGen one-shot mode**: `tollgate-keygen generate --app … --tier … [--count --days --features --notes --out --server --key]` with CI-friendly exit codes; KeyGen is also packable as a .NET global tool (`dotnet tool install -g Tollgate.KeyGen`).
- **Test suite** (`tests/Tollgate.Core.Tests`): tier logic, state predicates, machine fingerprint, encrypted cache round-trip, forged/expired/unsigned/wrong-machine/wrong-app token rejection, offline grace semantics, activation via stubbed HTTP, config discovery.
- **Server hardening**: rate limiting applied to all endpoints with 429 responses; CORS requires an explicit allow-list in Production; forwarded-headers support behind a reverse proxy; startup guard refuses placeholder `Jwt:Secret` / `Admin:Key` in Production; landing page version from assembly metadata; admin audit table; last-seen + last-app-version telemetry on license keys; `Apps:AllowAutoRegister` now enforced.
- **Paginated admin key listing** (`GET /api/admin/keys?page=&pageSize=`) via a new `KeyListResponse` DTO.
- **Central build metadata**: `Directory.Build.props` (version, authors, license, icon, deterministic builds, snupkg symbols), `Directory.Build.targets` (icon + SourceLink for packable projects), `Directory.Packages.props` (Central Package Management).
- **Repository meta files**: `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, `.editorconfig`, GitHub Actions CI workflow, package `icon.png`.
- `CancellationToken` support on all public async client APIs.

### Changed
- Multi-targeting: `Tollgate.Abstractions` → `netstandard2.0` + `net10.0`; `Tollgate.Licensing` → `net8.0` + `net10.0` + `net10.0-windows`; `Tollgate.AspNetCore` → `net8.0` + `net10.0`.
- **Admin authentication is header-only** (`X-Admin-Key`) on every admin endpoint; `AdminKey` was removed from all request DTOs.
- `SetFeaturesRequest` moved from the server's admin controller into `Tollgate.Abstractions.Dtos` (the KeyGen mirror `SetFeaturesCliRequest` is gone).
- Admin key comparison uses `CryptographicOperations.FixedTimeEquals` (no timing side channel).
- DI registration (`AddTollgate`) no longer creates a captive dependency: the singleton `LicenseClient` rents fresh `HttpClient`s from `IHttpClientFactory` per operation.
- `ResetMachineRequest` / `RevokeKeyRequest` / `VerifyTokenRequest` converted from positional to property-style records, with optional `AppId` scoping on revoke/reset.
- Windows fingerprint: registry `MachineGuid` fallback on plain TFM builds (WMI still used on `net10.0-windows`); fallback fingerprint no longer embeds `Environment.UserName` (PII).
- License state loaded offline is now derived from verified token claims, not from the cache file.
- Docker: `curl` installed in the runtime image so the compose healthcheck works; database path pinned to the `tollgate-data` volume via `Data__Path`; compose no longer uses the deprecated `version` key.
- Build scripts run the test suite and verify that all three packages were actually produced.
- `docker-compose.yml`, `systemd` unit and appsettings carry the new `Cors` / `Data` settings.

### Fixed
- **Restore blocker (NU1102)**: `Microsoft.Win32.Registry` was referenced at nonexistent versions (8.0.0 / 10.0.0 — the package's published line ends at 5.0.0 stable), so restore failed outright. Removed entirely: `Microsoft.Win32.RegistryKey` ships in the base shared framework of net8.0, net10.0 and net10.0-windows, so no package reference is needed.
- **Compile errors (masked by the restore failure)**: `RSA.ImportSubjectPublicKeyInfo` was called without the required `out int bytesRead` argument (CS7036, all TFMs); five admin action methods declared the optional `X-Admin-Key` parameter *before* required `[FromBody]` parameters (CS1737). Parameters were reordered — ASP.NET Core binds by name, so the HTTP contract is unchanged.
- **.NET 8 consumers (NU1605)**: `Microsoft.Extensions.Logging.Abstractions` was pinned at 8.0.1 for the net8.0 target, but `Microsoft.Extensions.Http 8.0.1` requires at least 8.0.2 — every .NET 8 consumer hit a package-downgrade error. Bumped to 8.0.2.
- `TollgateConfig.Load` now returns null on malformed or unreadable JSON instead of throwing — a corrupted `tollgate.json` can no longer crash a consumer's app at startup.
- Test project: global `Xunit` using + implicit usings added, `LicenseStore` namespace corrected, `Options.Create` disambiguated from the `TestInfra.Options` helper — 51/51 tests pass.
- `ForwardedHeadersOptions.KnownNetworks` (obsolete in .NET 10) replaced with `KnownIPNetworks`.
- XML documentation completed for all 18 previously undocumented public Abstractions members (tier values, state predicates, attribute and exception constructors) — full IntelliSense for consumers; the solution now builds with zero warnings.
- **Critical**: `LicenseClient` previously parsed cached JWTs with `ReadJwtToken` (no signature verification) — any local user could forge an Enterprise-tier cache entry. The client now verifies tokens with the same rigor as the server.
- Build scripts / Dockerfile referenced `Tollgate.Licensing` at the repository root instead of `src/`.
- Docker container listened on port 5000 while compose mapped and health-checked 7431.
- Mixed CRLF/LF line endings and stray UTF-8 BOMs normalized across the tree; mojibake in the sample app and package READMEs repaired; `.vs` directory and `*.csproj.user` files no longer ship.
- Client `HttpClient` timeout is no longer force-set on injected instances; `LicenseGate` client field is now `volatile` (no torn reads during reconfiguration).
- Tier claim parsing is case-insensitive and rejects undefined enum values.
- macOS fingerprint no longer risks a pipe deadlock (async read + enforced timeout).
- `TollgateConfig` JSON parser tolerates comments and trailing commas.

### Security
- See `SECURITY.md` for how to report vulnerabilities.

## [1.0.0] — pre-release baseline

Initial feature set: three-package split, license server, KeyGen CLI, sample app, deployment recipes.
