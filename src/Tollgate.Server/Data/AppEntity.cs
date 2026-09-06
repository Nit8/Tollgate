namespace Tollgate.Server.Data
{
    /// <summary>A registered application (multi-tenant support).</summary>
    public class AppEntity
    {
        public string AppId { get; set; } = "";   // PK
        public string DisplayName { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<LicenseKeyEntity> LicenseKeys { get; set; } = new();
    }
}
