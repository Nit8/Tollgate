using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tollgate.Server.Data;
using Tollgate.Server.Services;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;

namespace Tollgate.Server.Controllers;

// ─────────────────────────────────────────────────────────────
//  ADMIN CONTROLLER — protected by admin key header / body
//  POST /api/admin/generate
//  POST /api/admin/revoke
//  GET  /api/admin/keys
//  POST /api/admin/reset-machine
//  POST /api/admin/apps/register
//  GET  /api/admin/apps
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
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

    // ── AUTH ───────────────────────────────────────────────────
    private bool IsAdminAuthorized(string? providedKey)
    {
        var adminKey = _cfg["Admin:Key"] ?? "";
        return !string.IsNullOrEmpty(adminKey)
            && !string.IsNullOrEmpty(providedKey)
            && providedKey == adminKey;
    }

    private UnauthorizedObjectResult DenyAdmin() =>
        new(new { message = "Invalid admin key." });

    // ── GENERATE KEYS ─────────────────────────────────────────
    /// <summary>Generates 1–100 license keys for an app/tier.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateKeysResponse>> GenerateKeys(
        [FromBody] GenerateKeysRequest req)
    {
        if (!IsAdminAuthorized(req.AdminKey)) return DenyAdmin();
        if (string.IsNullOrWhiteSpace(req.AppId))
            return BadRequest(new { message = "AppId is required." });
        if (req.Count < 1 || req.Count > 100)
            return BadRequest(new { message = "Count must be between 1 and 100." });

        await EnsureAppExists(req.AppId);

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

        return Ok(new GenerateKeysResponse
        {
            Keys    = generated,
            Message = $"Generated {generated.Count} {req.Tier} key(s) for '{req.AppId}'."
        });
    }

    // ── UPDATE FEATURES ON A KEY ──────────────────────────────
    /// <summary>Add/remove features on an existing key without revoking.</summary>
    [HttpPost("set-features")]
    public async Task<IActionResult> SetFeatures([FromBody] SetFeaturesRequest req)
    {
        if (!IsAdminAuthorized(req.AdminKey)) return DenyAdmin();

        var entity = await _db.LicenseKeys.FirstOrDefaultAsync(k =>
            k.AppId == req.AppId && k.LicenseKey == req.LicenseKey.ToUpperInvariant());

        if (entity is null) return NotFound(new { message = "Key not found." });
        entity.SetFeatures(req.Features);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Features updated.", features = entity.FeaturesList });
    }

    // ── REVOKE KEY ────────────────────────────────────────────
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeKey([FromBody] RevokeKeyRequest req)
    {
        if (!IsAdminAuthorized(req.AdminKey)) return DenyAdmin();

        var entity = await _db.LicenseKeys.FirstOrDefaultAsync(k =>
            k.LicenseKey == req.LicenseKey.ToUpperInvariant());
        if (entity is null) return NotFound(new { message = "Key not found." });

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        _log.LogInformation("Revoked key {Key}", req.LicenseKey);
        return Ok(new { message = "Key revoked." });
    }

    // ── LIST ALL KEYS (filtered by app / tier / active) ───────
    [HttpGet("keys")]
    public async Task<ActionResult<List<LicenseKeyInfo>>> ListKeys(
        [FromHeader(Name = "X-Admin-Key")] string adminKey,
        [FromQuery] string? appId = null,
        [FromQuery] LicenseTier? tier = null,
        [FromQuery] bool? active = null)
    {
        if (!IsAdminAuthorized(adminKey)) return DenyAdmin();

        var q = _db.LicenseKeys.AsQueryable();
        if (!string.IsNullOrEmpty(appId))   q = q.Where(k => k.AppId == appId);
        if (tier.HasValue)                   q = q.Where(k => k.Tier == tier.Value);
        if (active.HasValue)                 q = q.Where(k => k.IsActive == active.Value);

        var keys = await q.OrderByDescending(k => k.CreatedAt).ToListAsync();
        return Ok(keys.Select(k => new LicenseKeyInfo
        {
            LicenseKey  = k.LicenseKey,
            AppId       = k.AppId,
            Tier        = k.Tier,
            Features    = k.FeaturesList,
            IsActive    = k.IsActive,
            MachineId   = k.MachineId,
            CreatedAt   = k.CreatedAt,
            ActivatedAt = k.ActivatedAt,
            ExpiresAt   = k.ExpiresAt,
            UseCount    = k.UseCount,
            Notes       = k.Notes
        }).ToList());
    }

    // ── RESET MACHINE BINDING ─────────────────────────────────
    [HttpPost("reset-machine")]
    public async Task<IActionResult> ResetMachine([FromBody] ResetMachineRequest req)
    {
        if (!IsAdminAuthorized(req.AdminKey)) return DenyAdmin();

        var entity = await _db.LicenseKeys.FirstOrDefaultAsync(k =>
            k.LicenseKey == req.LicenseKey.ToUpperInvariant());
        if (entity is null) return NotFound(new { message = "Key not found." });

        entity.MachineId   = null;
        entity.ActivatedAt = null;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Machine binding reset. Key can be activated on a new machine." });
    }

    // ── APP REGISTRATION ─────────────────────────────────────
    [HttpPost("apps/register")]
    public async Task<IActionResult> RegisterApp([FromBody] RegisterAppRequest req)
    {
        if (!IsAdminAuthorized(req.AdminKey)) return DenyAdmin();
        if (string.IsNullOrWhiteSpace(req.AppId))
            return BadRequest(new { message = "AppId is required." });

        await EnsureAppExists(req.AppId, req.DisplayName);
        return Ok(new { message = $"App '{req.AppId}' registered." });
    }

    [HttpGet("apps")]
    public async Task<ActionResult<List<AppInfo>>> ListApps(
        [FromHeader(Name = "X-Admin-Key")] string adminKey)
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
}

// ── Request DTO local to this controller ──────────────────────
public record SetFeaturesRequest(
    string LicenseKey,
    string AppId,
    List<string> Features,
    string AdminKey);
