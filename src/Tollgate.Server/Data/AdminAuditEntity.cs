namespace Tollgate.Server.Data
{
    /// <summary>
    /// One row per admin API call — a persistent, queryable audit trail
    /// (console logs are not history). Written by AdminController.
    /// </summary>
    public class AdminAuditEntity
    {
        /// <summary>Surrogate primary key.</summary>
        public int Id { get; set; }

        /// <summary>The admin operation, e.g. "generate", "revoke".</summary>
        public string Action { get; set; } = "";

        /// <summary>The license key involved, when applicable.</summary>
        public string? LicenseKey { get; set; }

        /// <summary>The app involved, when applicable.</summary>
        public string? AppId { get; set; }

        /// <summary>Short detail line (counts, features, outcome).</summary>
        public string? Detail { get; set; }

        /// <summary>UTC timestamp of the operation.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
