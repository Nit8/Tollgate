using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;
using Tollgate.Server.Data;
using Tollgate.Server.Services;

namespace Tollgate.Server.Controllers;

// ─────────────────────────────────────────────────────────────
//  LICENSE CONTROLLER  — public endpoint (no admin key required)
//  POST /api/license/validate
//  POST /api/license/verify-token
//  GET  /api/license/health
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/license")]
public class LicenseController : ControllerBase
{
    private readonly LicenseDbContext _db;
    private readonly TokenService     _tokens;
    private readonly ILogger<LicenseController> _log;

    public LicenseController(LicenseDbContext db, TokenService tokens,
                              ILogger<LicenseController> log)
    {
        _db     = db;
        _tokens = tokens;
        _log    = log;
    }

    // ── VALIDATE KEY ──────────────────────────────────────────
    /// <summary>
    /// Called by client apps on startup or activation.
    /// Returns tier + features + a signed JWT the client caches locally.
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ValidateLicenseResponse>> Validate(
        [FromBody] ValidateLicenseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LicenseKey) ||
            string.IsNullOrWhiteSpace(req.MachineId) ||
            string.IsNullOrWhiteSpace(req.AppId))
        {
            return BadRequest(Fail("LicenseKey, MachineId, and AppId are required."));
        }

        var normalizedKey = req.LicenseKey.Trim().ToUpperInvariant();

        var entity = await _db.LicenseKeys
            .FirstOrDefaultAsync(k => k.AppId == req.AppId && k.LicenseKey == normalizedKey);

        if (entity is null)
        {
            _log.LogWarning("Unknown key attempt: app={App} key={Key}", req.AppId, normalizedKey);
            return Ok(Fail("License key not found for this application."));
        }

        if (!entity.IsActive)
            return Ok(Fail("This license key has been revoked."));

        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value < DateTime.UtcNow)
            return Ok(Fail("This license key has expired."));

        // ── Machine binding ───────────────────────────────────
        if (entity.MachineId is null)
        {
            entity.MachineId   = req.MachineId;
            entity.ActivatedAt = DateTime.UtcNow;
            _log.LogInformation("Key {Key} activated on machine {MID}",
                                normalizedKey, req.MachineId);
        }
        else if (entity.MachineId != req.MachineId)
        {
            _log.LogWarning("Key {Key} rejected — machine mismatch", normalizedKey);
            return Ok(Fail(
                "This license is already activated on a different machine. " +
                "Contact support to transfer your license."));
        }

        entity.UseCount++;
        await _db.SaveChangesAsync();

        var features = entity.FeaturesList;
        var token = _tokens.IssueToken(
            normalizedKey, req.AppId, entity.Tier, features,
            req.MachineId, entity.ExpiresAt);

        return Ok(new ValidateLicenseResponse
        {
            IsValid   = true,
            Tier      = entity.Tier,
            Features  = features,
            Message   = $"License valid — {entity.Tier} tier activated.",
            ExpiresAt = entity.ExpiresAt,
            Token     = token,
            AppId     = req.AppId
        });
    }

    // ── VERIFY CACHED TOKEN ──────────────────────────────────
    [HttpPost("verify-token")]
    public ActionResult<ValidateLicenseResponse> VerifyToken(
        [FromBody] VerifyTokenRequest req)
    {
        var principal = _tokens.ValidateToken(req.Token);
        if (principal is null)
            return Ok(Fail("Cached token is invalid or expired. Please re-activate."));

        var tierStr = principal.FindFirst("tier")?.Value ?? "None";
        var mid     = principal.FindFirst("mid")?.Value  ?? "";
        var app     = principal.FindFirst("app")?.Value  ?? "";
        var feat    = principal.FindFirst("feat")?.Value ?? "";

        if (mid != req.MachineId)
            return Ok(Fail("Token machine mismatch."));

        if (!string.IsNullOrEmpty(app) && app != req.AppId)
            return Ok(Fail("Token app mismatch."));

        Enum.TryParse(tierStr, out LicenseTier tier);

        return Ok(new ValidateLicenseResponse
        {
            IsValid = true,
            Tier    = tier,
            Features= feat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .ToList(),
            Message = $"Cached token valid — {tier} tier.",
            Token   = req.Token,
            AppId   = app
        });
    }

    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "ok", service = "Tollgate.Server", time = DateTime.UtcNow });

    private static ValidateLicenseResponse Fail(string msg) =>
        new() { IsValid = false, Tier = LicenseTier.None, Message = msg };
}
