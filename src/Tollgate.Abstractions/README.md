# Tollgate.Abstractions

Shared contracts for the [Tollgate](https://github.com/your-org/tollgate) SaaS licensing system.

## Install

```bash
dotnet add package Tollgate.Abstractions
```

## What's inside

- `RequireFeatureAttribute` — data annotation that gates a method / class / property behind a feature flag.
- `RequireTierAttribute` — data annotation that gates behind a tier (Basic / Pro / Enterprise).
- `LicenseTier` enum
- `LicenseState` record — current license snapshot
- Request / response DTOs (`ValidateLicenseRequest`, `ValidateLicenseResponse`, …)
- `LicenseRequiredException`

Pair this with `Tollgate.Licensing` (the client) and `Tollgate.Server` (the self-hostable license / keygen server).

See the [main README](https://github.com/your-org/tollgate) for full docs.
