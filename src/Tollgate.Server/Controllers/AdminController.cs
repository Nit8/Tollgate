using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;
using Tollgate.Server.Data;
using Tollgate.Server.Services;

namespace Tollgate.Server.Controllers;

// ─────────────────────────────────────────────────────────────
//  ADMIN CONTROLLER — protected by the X-Admin-Key header
//  (header only on every endpoint: body credentials leak into
//  proxy logs and exception reports far more easily)
//
//  POST /api/admin/generate
//  POST /api/admin/set-features
//  POST /api/admin/revoke
//  POST /api/admin/reset-machine
//  POST /api/admin/apps/register
//  GET  /api/admin/keys        (paginated)
//  GET  /api/admin/apps
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/admin")]
[EnableRateLimiting("api")]
public class AdminController : ControllerBase
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 500;

    private readonly LicenseDbContext _db;
    private readonly IConfiguration   _cfg;
    private readonly ILogger<AdminController> _log;

    public AdminController(LicenseDbContext db, IConfiguration cfg,
                           ILogger<AdminController> log)
    {
        _db  = db;
        _cfg = cfg;
        _log = log;
    }

    // ── AUTH (constant-time comparison) ───────────────────────
    private bool IsAdminAuthorized(string? providedKey)
    {
        var adminKey = _cfg["Admin:Key"] ?? "";
        if (adminKey.Length == 0 || string.IsNullOrEmpty(providedKey)) return false;

        // FixedTimeEquals over fixed-length byte arrays — no timing side
        // channel for key recovery. Length is compared separately (leaking
        // the configured key's length is acceptable).
        var expected = Encoding.UTF8.GetBytes(adminKey);
        var provided = Encoding.UTF8.GetBytes(providedKey);
        return expected.Length == provided.Length
            && CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private UnauthorizedObjectResult DenyAdmin()
    {
        _log?.LogWarning("Rejected admin call from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
        return new UnauthorizedObjectResult(new { message = "Invalid admin key." });
    }

    // ── GENERATE KEYS ─────────────────────────────────────────
    /// <summary>Generates 1–100 license keys for an app/tier.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateKeysResponse>> GenerateKeys(
        [FromBody] GenerateKeysRequest req,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();
        if (string.IsNullOrWhiteSpace(req.AppId))
            return BadRequest(new { message = "AppId is required." });
        if (req.Count < 1 || req.Count > 100)
            return BadRequest(new { message = "Count must be between 1 and 100." });

        if (!await TryEnsureAppExists(req.AppId))
            return BadRequest(new
            {
                message = $"App '{req.AppId}' is not registered and Apps:AllowAutoRegister is disabled. " +
                          "Register it first via /api/admin/apps/register."
            });

        var generated = new List<string>();
        for (int i = 0; i < req.Count; i++)
        {
            var rawKey = LicenseKeyGenerator.Generate(req.Tier);
            while (await _db.LicenseKeys.AnyAsync(k =>
                k.AppId == req.AppId && k.LicenseKey == rawKey))
            {
                rawKey = LicenseKeyGenerator.Generate(req.Tier);
            }

            var entity = new LicenseKeyEntity
            {
                LicenseKey = rawKey,
                AppId      = req.AppId,
                Tier       = req.Tier,
                IsActive   = true,
                CreatedAt  = DateTime.UtcNow,
                ExpiresAt  = req.ValidDays.HasValue
                                ? DateTime.UtcNow.AddDays(req.ValidDays.Value)
                                : null,
                Notes      = req.Notes
            };
            entity.SetFeatures(req.Features);

            _db.LicenseKeys.Add(entity);
            generated.Add(rawKey);
        }

        await _db.SaveChangesAsync();

        _log.LogInformation("Generated {Count} {Tier} keys for app {App}",
                             req.Count, req.Tier, req.AppId);
        await AuditAsync("generate", appId: req.AppId,
            detail: $"{req.Count} x {req.Tier}, features=[{string.Join(",", req.Features)}]");

        return Ok(new GenerateKeysResponse
        {
            Keys    = generated,
            Message = $"Generated {generated.Count} {req.Tier} key(s) for '{req.AppId}'."
        });
    }

    // ── UPDATE FEATURES ON A KEY ──────────────────────────────
    /// <summary>Replace the feature list on an existing key without revoking.</summary>
    [HttpPost("set-features")]
    public async Task<IActionResult> SetFeatures(
        [FromBody] SetFeaturesRequest req,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();

        var entity = await _db.LicenseKeys.FirstOrDefaultAsync(k =>
            k.AppId == req.AppId && k.LicenseKey == req.LicenseKey.ToUpperInvariant());

        if (entity is null) return NotFound(new { message = "Key not found." });
        entity.SetFeatures(req.Features);
        await _db.SaveChangesAsync();

        await AuditAsync("set-features", req.LicenseKey, req.AppId,
            detail: $"features=[{string.Join(",", req.Features)}]");

        return Ok(new { message = "Features updated.", features = entity.FeaturesList });
    }

    // ── REVOKE KEY ────────────────────────────────────────────
    /// <summary>Revoke a key (scoped by app when AppId is provided).</summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeKey(
        [FromBody] RevokeKeyRequest req,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();

        var entity = await FindKeyScopedAsync(req.LicenseKey, ScopeOrNull(req.AppId));
        if (entity is null) return NotFound(new { message = "Key not found." });

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        _log.LogInformation("Revoked key {Key} (app {App})", req.LicenseKey, entity.AppId);
        await AuditAsync("revoke", entity.LicenseKey, entity.AppId);
        return Ok(new { message = "Key revoked." });
    }

    // ── LIST KEYS (filtered + paginated) ───────────────────────
    /// <summary>
    /// List keys with optional app / tier / active filters and paging.
    /// Defaults: page 1, pageSize 100 (max 500).
    /// </summary>
    [HttpGet("keys")]
    public async Task<ActionResult<KeyListResponse>> ListKeys(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null,
        [FromQuery] string? appId = null,
        [FromQuery] LicenseTier? tier = null,
        [FromQuery] bool? active = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var q = _db.LicenseKeys.AsQueryable();
        if (!string.IsNullOrEmpty(appId))   q = q.Where(k => k.AppId == appId);
        if (tier.HasValue)                   q = q.Where(k => k.Tier == tier.Value);
        if (active.HasValue)                 q = q.Where(k => k.IsActive == active.Value);

        var total = await q.CountAsync();
        var keys = await q.OrderByDescending(k => k.CreatedAt)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync();

        return Ok(new KeyListResponse
        {
            Keys     = keys.Select(k => new LicenseKeyInfo
            {
                LicenseKey     = k.LicenseKey,
                AppId          = k.AppId,
                Tier           = k.Tier,
                Features       = k.FeaturesList,
                IsActive       = k.IsActive,
                MachineId      = k.MachineId,
                CreatedAt      = k.CreatedAt,
                ActivatedAt    = k.ActivatedAt,
                ExpiresAt      = k.ExpiresAt,
                LastSeenAt     = k.LastSeenAt,
                LastAppVersion = k.LastAppVersion,
                UseCount       = k.UseCount,
                Notes          = k.Notes
            }).ToList(),
            Page     = page,
            PageSize = pageSize,
            Total    = total
        });
    }

    // ── RESET MACHINE BINDING ─────────────────────────────────
    /// <summary>Clear machine binding so the key can activate elsewhere.</summary>
    [HttpPost("reset-machine")]
    public async Task<IActionResult> ResetMachine(
        [FromBody] ResetMachineRequest req,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();

        var entity = await FindKeyScopedAsync(req.LicenseKey, ScopeOrNull(req.AppId));
        if (entity is null) return NotFound(new { message = "Key not found." });

        var previousMachine = entity.MachineId;
        entity.MachineId   = null;
        entity.ActivatedAt = null;
        await _db.SaveChangesAsync();

        await AuditAsync("reset-machine", entity.LicenseKey, entity.AppId,
            detail: $"previous machine={previousMachine ?? "(none)"}");
        return Ok(new { message = "Machine binding reset. Key can be activated on a new machine." });
    }

    // ── APP REGISTRATION ─────────────────────────────────────
    /// <summary>Register a new app (multi-tenant).</summary>
    [HttpPost("apps/register")]
    public async Task<IActionResult> RegisterApp(
        [FromBody] RegisterAppRequest req,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();
        if (string.IsNullOrWhiteSpace(req.AppId))
            return BadRequest(new { message = "AppId is required." });

        await EnsureAppExists(req.AppId, req.DisplayName);
        await AuditAsync("register-app", appId: req.AppId, detail: req.DisplayName);
        return Ok(new { message = $"App '{req.AppId}' registered." });
    }

    /// <summary>List all registered apps.</summary>
    [HttpGet("apps")]
    public async Task<ActionResult<List<AppInfo>>> ListApps(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();
        var apps = await _db.Apps.Include(a => a.LicenseKeys).ToListAsync();
        return Ok(apps.Select(a => new AppInfo
        {
            AppId       = a.AppId,
            DisplayName = a.DisplayName,
            CreatedAt   = a.CreatedAt,
            KeyCount    = a.LicenseKeys.Count
        }).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>Find a key by its string, optionally scoped to an app.</summary>
    private Task<LicenseKeyEntity?> FindKeyScopedAsync(string licenseKey, string? appId)
    {
        var normalized = licenseKey.Trim().ToUpperInvariant();
        return appId is null
            ? _db.LicenseKeys.FirstOrDefaultAsync(k => k.LicenseKey == normalized)
            : _db.LicenseKeys.FirstOrDefaultAsync(k => k.AppId == appId && k.LicenseKey == normalized);
    }

    /// <summary>
    /// Register the app unless auto-registration is disabled. Returns false
    /// when the app is unknown and Apps:AllowAutoRegister is false.
    /// </summary>
    private async Task<bool> TryEnsureAppExists(string appId)
    {
        if (await _db.Apps.AnyAsync(a => a.AppId == appId)) return true;
        if (!_cfg.GetValue("Apps:AllowAutoRegister", true)) return false;
        await EnsureAppExists(appId);
        return true;
    }

    /// <summary>Normalize an optional app scope (blank → null = global lookup).</summary>
    private static string? ScopeOrNull(string? appId) =>
        string.IsNullOrWhiteSpace(appId) ? null : appId;

    private async Task EnsureAppExists(string appId, string? displayName = null)
    {
        if (await _db.Apps.AnyAsync(a => a.AppId == appId)) return;
        _db.Apps.Add(new AppEntity
        {
            AppId       = appId,
            DisplayName = displayName ?? appId
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Write one audit row (fire-and-forget on failure paths).</summary>
    private async Task AuditAsync(string action, string? licenseKey = null,
                                  string? appId = null, string? detail = null)
    {
        try
        {
            _db.AdminAudit.Add(new AdminAuditEntity
            {
                Action     = action,
                LicenseKey = licenseKey,
                AppId      = appId,
                Detail     = detail,
                Timestamp  = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Auditing must never break the admin operation itself.
            _log?.LogError(ex, "Failed to persist audit entry for {Action}", action);
        }
    }
}
