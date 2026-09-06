using System;
using System.Collections.Generic;
using System.Text;

namespace Tollgate.Abstractions.Dtos
{
    /// <summary>Admin: summary of a registered application.</summary>
    public record AppInfo
    {
        /// <summary>The application's unique ID.</summary>
        public string AppId { get; init; } = "";

        /// <summary>Human-friendly display name.</summary>
        public string DisplayName { get; init; } = "";

        /// <summary>UTC time the app was registered.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Number of license keys issued for this app.</summary>
        public int KeyCount { get; init; }
    }
}
