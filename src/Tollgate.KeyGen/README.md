# Tollgate.KeyGen

Admin CLI for the [Tollgate](https://github.com/your-org/tollgate) license server: generate, revoke and manage license keys and features.

## Install as a .NET global tool

```bash
dotnet tool install -g Tollgate.KeyGen
tollgate-keygen --help
```

(Or run from source: `dotnet run --project src/Tollgate.KeyGen`.)

## Usage

```
tollgate-keygen                                   interactive menu
tollgate-keygen init                              create/edit tollgate.json
tollgate-keygen generate [options]                one-shot key generation (CI-friendly)
```

### One-shot generate

```
tollgate-keygen generate --app my-app --tier Pro --count 10 \
    [--days 30] [--features export-pdf,ai] [--notes "..."] \
    [--out keys.txt] [--server http://...] [--key ADMINKEY]
```

Keys print to stdout (one per line) so they can be piped; `--out` additionally writes an annotated text file. The exit code is non-zero on failure, so CI pipelines fail loudly.

Tiers: `Free`, `Basic`, `Pro`, `Enterprise`, `None` (issues `TRIAL-…` keys).

## Config discovery (first match wins)

1. `--server` / `--key` command-line options (generate mode)
2. `TOLLGATE_SERVER` / `TOLLGATE_ADMIN_KEY` environment variables
3. `tollgate.json` — auto-discovered from the standard search paths:
   `$TOLLGATE_CONFIG`, the binary directory, the working directory, or the
   per-OS user config dir (`~/.config/tollgate/` on Linux,
   `%APPDATA%\Tollgate` on Windows)

Run `tollgate-keygen init` once to create the config file interactively. **The file contains secrets — add it to `.gitignore`.**

## Authentication

All admin calls send the `X-Admin-Key` header (never a body credential), matching the server's `Admin:Key` setting.
