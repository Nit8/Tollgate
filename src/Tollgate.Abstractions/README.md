# Tollgate.Abstractions

Shared contracts for the [Tollgate](https://github.com/your-org/tollgate) SaaS licensing system.

## Install

```bash
dotnet add package Tollgate.Abstractions
```

Targets **netstandard2.0** and net10.0 — referenceable from every .NET runtime of the last decade, including .NET Framework 4.6.2+ and .NET 8.

## What's inside

- `RequireFeatureAttribute` — data annotation that gates a method / class / property behind a feature flag.
- `RequireTierAttribute` — data annotation that gates behind a tier (Free / Basic / Pro / Enterprise).
- `RequireTrialAttribute` — data annotation that gates behind a valid trial (None-tier key).
- `LicenseTier` enum — ordered tiers (`Meets()` comparison helper included)
- `LicenseState` — current license snapshot (`HasFeature`, `MeetsTier`, `IsTrial`, …)
- Request / response DTOs (`ValidateLicenseRequest`, `ValidateLicenseResponse`, …)
- `LicenseRequiredException`, `LicenseNotConfiguredException`

Pair this with `Tollgate.Licensing` (the client) and `Tollgate.Server` (the self-hostable license / keygen server).

See the [main README](https://github.com/your-org/tollgate) for full docs.
