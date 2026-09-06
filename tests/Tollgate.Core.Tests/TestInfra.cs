using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tollgate.Licensing;

namespace Tollgate.Core.Tests;

/// <summary>
/// Shared helpers: token issuance (mirroring the server's TokenService) and
/// stubbed HTTP so the client can be tested without a live server.
/// </summary>
public static class TestInfra
{
    public const string AppId = "test-app";
    public const string Secret = "0123456789abcdef0123456789abcdef"; // 32 chars
    public const string WrongSecret = "ffffffffffffffffffffffffffffffff";
    public const string LicenseKey = "PRO-TEST-0000-0000-0001";

    /// <summary>Issue a JWT the way the server's TokenService does.</summary>
    public static string IssueToken(
        string secret = Secret,
        string tier = "Pro",
        string app = AppId,
        string? machineId = null,
        DateTime? expires = null,
        string features = "export-pdf",
        string issuer = "TollgateServer",
        string audience = "TollgateClient",
        bool sign = true)
    {
        var claims = new List<Claim>
        {
            new("lic", LicenseKey),
            new("app", app),
            new("tier", tier),
            new("mid", machineId ?? MachineFingerprint.Get()),
            new("feat", features),
        };

        SigningCredentials? creds = null;
        if (sign)
        {
            creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256);
        }

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    /// <summary>Build a ValidateLicenseResponse JSON body like the server's.</summary>
    public static HttpContent ResponseBody(string token, bool isValid = true,
        string tier = "Pro", string message = "License valid.")
        => new StringContent(
            JsonSerializer.Serialize(new
            {
                isValid,
                tier,
                features = new[] { "export-pdf" },
                message,
                expiresAt = (DateTime?)null,
                token,
                appId = AppId
            }), Encoding.UTF8, "application/json");

    /// <summary>HttpClient whose transport is fully controlled by the test.</summary>
    public static (HttpClient Http, StubHandler Handler) StubbedClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHandler(responder);
        return (new HttpClient(handler) { BaseAddress = new Uri("http://localhost:9") }, handler);
    }

    public static HttpClient UnreachableClient() =>
        StubbedClient((_, _) => throw new HttpRequestException("no network")).Http;

    /// <summary>Fresh options pointing at an isolated cache directory.</summary>
    public static TollgateOptions Options(
        Action<TollgateOptions>? tweak = null,
        string? secret = Secret)
    {
        var dir = Path.Combine(Path.GetTempPath(), "tollgate-tests", Guid.NewGuid().ToString("N"));
        var options = new TollgateOptions
        {
            AppId = AppId,
            ServerUrl = "http://localhost:9",
            SharedSecret = secret ?? "",
            OfflineGraceDays = 7,
            CacheDirectory = dir
        };
        tweak?.Invoke(options);
        return options;
    }

    public static LicenseClient Client(HttpClient http, TollgateOptions options) =>
        new(http, Options.Create(options));
}

/// <summary>HttpMessageHandler that records calls and answers from a delegate.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    public int Calls { get; private set; }

    public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        => _responder = responder;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return await _responder(request, cancellationToken);
    }
}
