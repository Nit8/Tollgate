using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tollgate.Licensing
{
    // ─────────────────────────────────────────────────────────────
    //  TOLLGATE CONFIG — config-file discovery + loading
    //
    //  Tollgate can be configured three ways:
    //
    //    1. Programmatic  — LicenseGate.Configure(o => { ... });
    //    2. appsettings   — builder.Services.AddTollgate(config.GetSection("Tollgate"));
    //    3. tollgate.json — a standalone config file, auto-discovered
    //                       (this file's purpose)
    //
    //  tollgate.json is searched in this order (first match wins):
    //
    //    a) Path in TOLLGATE_CONFIG environment variable
    //    b) ./tollgate.json              (next to the app binary)
    //    c) ~/.tollgate/tollgate.json   (user-wide, shared)
    //    d) Platform-specific user dir:
    //         Windows: %APPDATA%/Tollgate/tollgate.json
    //         Linux:   ~/.config/tollgate/tollgate.json
    //         macOS:   ~/Library/Application Support/Tollgate/tollgate.json
    //
    //  This is the file the KeyGen CLI uses for its server URL + admin key,
    //  and that your own app can use for its client options. One file
    //  covers both use cases — drop it in ~/.tollgate/ once and every
    //  Tollgate-aware tool on that machine picks it up.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Shape of the tollgate.json file. Mirrors <see cref="TollgateOptions"/>,
    /// plus an extra <see cref="AdminKey"/> field used only by the KeyGen CLI.
    /// </summary>
    public sealed class TollgateConfig
    {
        /// <summary>Server URL, e.g. https://license.myapp.com.</summary>
        public string ServerUrl { get; set; } = "http://localhost:5000";

        /// <summary>App ID. Unique per product.</summary>
        public string AppId { get; set; } = "";

        /// <summary>App version reported to the server.</summary>
        public string AppVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Admin key — required by the KeyGen CLI to call /api/admin/*.
        /// Client apps do NOT need this; only the KeyGen CLI reads it.
        /// </summary>
        public string AdminKey { get; set; } = "";

        /// <summary>RSA public key (PEM) for asymmetric JWT verification. Optional.</summary>
        public string PublicKey { get; set; } = "";

        /// <summary>HMAC shared secret for symmetric JWT verification. Optional.</summary>
        public string SharedSecret { get; set; } = "";

        /// <summary>Cache file name.</summary>
        public string CacheFile { get; set; } = "license.dat";

        /// <summary>Days a cached token is honored offline.</summary>
        public int OfflineGraceDays { get; set; } = 7;

        /// <summary>When true, the app runs even with no license (free mode).</summary>
        public bool AllowFreeMode { get; set; } = true;

        // ── Loading ──────────────────────────────────────────────

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Discover and load the first tollgate.json that exists, in the
        /// search order documented at the top of this file. Returns null
        /// if none found.
        /// </summary>
        public static TollgateConfig? Discover()
        {
            foreach (var path in GetSearchPaths())
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return Load(path);
                    }
                    catch
                    {
                        // malformed file — skip and try next location
                    }
                }
            }
            return null;
        }

        /// <summary>Load a tollgate.json from an explicit path.</summary>
        public static TollgateConfig? Load(string path)
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TollgateConfig>(json, JsonOpts);
        }

        /// <summary>
        /// Discover where the config SHOULD live. The first path that
        /// exists is returned; if none exist, the first writable location
        /// is returned (so a caller can write a template there).
        /// </summary>
        public static string GetDefaultPath()
        {
            var paths = GetSearchPaths();
            foreach (var p in paths)
                if (File.Exists(p)) return p;

            // None exists yet — return the first user-wide location
            // (not the CWD, which may be transient).
            return paths.Length > 2 ? paths[2] : paths[0];
        }

        /// <summary>
        /// Save this config to disk at the given path. Creates parent
        /// directories if needed.
        /// </summary>
        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Write a starter template to the given path, with helpful
        /// comments pointing the user at what to fill in.
        /// </summary>
        public static void WriteTemplate(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Hand-written JSON with comments — System.Text.Json doesn't
            // support comments by default, so we use // via the
            // "read with comments" extension later. For now, just write
            // clean JSON.
            var template = new TollgateConfig
            {
                ServerUrl = "http://localhost:5000",
                AppId = "my-app",
                AppVersion = "1.0.0",
                AdminKey = "REPLACE_WITH_YOUR_ADMIN_KEY",
                CacheFile = "license.dat",
                OfflineGraceDays = 7,
                AllowFreeMode = true,
            };
            template.Save(path);
        }

        /// <summary>
        /// Returns the ordered list of paths to search for tollgate.json.
        /// Exposed publicly so consumers can show "I loaded config from X"
        /// in their diagnostics.
        /// </summary>
        public static string[] GetSearchPaths()
        {
            var paths = new List<string>();

            // (a) Explicit override via env var
            var env = Environment.GetEnvironmentVariable("TOLLGATE_CONFIG");
            if (!string.IsNullOrEmpty(env)) paths.Add(Path.GetFullPath(env));

            // (b) Next to the running app's base directory
            paths.Add(Path.Combine(AppContext.BaseDirectory, "tollgate.json"));

            // (c) Current working directory (useful for KeyGen CLI)
            paths.Add(Path.Combine(Environment.CurrentDirectory, "tollgate.json"));

            // (d) User-wide shared location
            var userDir = GetUserConfigDirectory();
            if (!string.IsNullOrEmpty(userDir))
                paths.Add(Path.Combine(userDir, "tollgate.json"));

            return paths.ToArray();
        }

        /// <summary>Convert this config to a TollgateOptions (drops AdminKey).</summary>
        public TollgateOptions ToOptions() => new()
        {
            ServerUrl = ServerUrl,
            AppId = AppId,
            AppVersion = AppVersion,
            PublicKey = PublicKey,
            SharedSecret = SharedSecret,
            CacheFile = CacheFile,
            OfflineGraceDays = OfflineGraceDays,
            AllowFreeMode = AllowFreeMode,
        };

        private static string? GetUserConfigDirectory()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Tollgate");
                }
                if (OperatingSystem.IsMacOS())
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return Path.Combine(home, "Library", "Application Support", "Tollgate");
                }
                if (OperatingSystem.IsLinux())
                {
                    // Respect XDG_CONFIG_HOME if set
                    var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (!string.IsNullOrEmpty(xdg))
                        return Path.Combine(xdg, "tollgate");
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return Path.Combine(home, ".config", "tollgate");
                }
            }
            catch { }
            return null;
        }
    }
}