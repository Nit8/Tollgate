# Security Policy

Tollgate is a licensing toolkit — its whole value proposition is resisting
unauthorized use, so security reports are treated with priority.

## Reporting a vulnerability

**Do not open a public GitHub issue for security problems.**

1. Email **nitesh.singh1423.nl@gmail.com** (or use GitHub's private
   vulnerability reporting on the repository).
2. Include: affected package + version, a description, and — if possible —
   a minimal repro or PoC.
3. You will get an acknowledgment within 72 hours and a status update at
   least every 7 days until resolution.

Please give us 90 days to address the issue before public disclosure. We
credit reporters in the CHANGELOG and the release notes (opt-out available).

## Scope

In scope:
- `Tollgate.Licensing` (client) — token verification, cache encryption,
  fingerprint handling, config parsing.
- `Tollgate.Server` — authentication, rate limiting, token issuance,
  data handling.
- `Tollgate.AspNetCore` — filter enforcement.

Out of scope (accepted limitations, documented in the README threat model):
- A determined attacker patching licensing checks out of a compiled binary
  with a decompiler (all client-side enforcement is defeatable at that
  level; the defense is server-side truth).
- Compromised machines of legitimate licensees (DPAPI/AES-GCM cache
  encryption is a tamper-and-casual-piracy defense, not full DRM).

## Supported versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | yes       |

## Disclosure timeline

1. Report received → acknowledgment (≤ 72 h).
2. Triage + fix on a private branch.
3. Coordinated release + CVE (if warranted) + CHANGELOG entry.
