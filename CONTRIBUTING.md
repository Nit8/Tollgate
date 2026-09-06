# Contributing to Tollgate

Thanks for considering a contribution!

## Getting started

```bash
git clone https://github.com/your-org/tollgate.git
cd tollgate
./build.sh          # restore + build + test + pack
```

Requirements: .NET 10 SDK (the solution uses the `Tollgate.slnx` XML format —
Visual Studio 17.13+ or any recent `dotnet` CLI).

## Ground rules

- **Run the tests.** `dotnet test` must pass. New features for the client
  library should come with tests — especially anything touching token
  verification, the cache, or the grace logic (see
  `tests/Tollgate.Core.Tests`).
- **Keep the public API stable.** This is a published NuGet package family;
  follow SemVer. Additive changes are minor; breaking changes need a major
  version and a CHANGELOG entry.
- **One PR per concern.** Small, reviewable diffs land faster.
- **No secrets in the repo.** `tollgate.json`, `*.db`, `.env` are gitignored
  for a reason.
- **Docs count.** If you change public behavior, update the README and the
  XML doc comments — `GenerateDocumentationFile` is enabled and CS1591
  warnings are treated as errors in CI.

## Project layout

| Path | What it is |
|---|---|
| `src/Tollgate.Abstractions` | Attributes, enums, DTOs (netstandard2.0 + net10.0) |
| `src/Tollgate.Licensing` | The client library (multi-targeted) |
| `src/Tollgate.AspNetCore` | MVC filter integration |
| `src/Tollgate.Server` | Self-hosted license server |
| `src/Tollgate.KeyGen` | Admin CLI / .NET global tool |
| `tests/Tollgate.Core.Tests` | xUnit test suite |
| `deploy/` | Docker / systemd / nginx recipes |

Package versions and dependencies are managed centrally in
`Directory.Packages.props`; shared NuGet metadata lives in
`Directory.Build.props`.

## Commit style

Conventional-ish, imperative mood: `Add deactivation endpoint`,
`Fix cache grace calculation`. Squash before merge if the history is noisy.

## Reporting bugs / security issues

Bugs: open a GitHub issue with repro steps and the package version.
Security: **do not open a public issue** — see [SECURITY.md](SECURITY.md).
