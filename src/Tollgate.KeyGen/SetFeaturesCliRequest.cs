namespace Tollgate.KeyGen
{
    /// <summary>Cli-side payload for /api/admin/set-features (mirrors server DTO).</summary>
    public record SetFeaturesCliRequest(string LicenseKey, string AppId, List<string> Features, string AdminKey);
}