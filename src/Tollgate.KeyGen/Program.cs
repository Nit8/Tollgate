using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Tollgate.Abstractions.Dtos;
using Tollgate.Abstractions.Enums;
using Tollgate.Licensing;

namespace Tollgate.KeyGen
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            // ─────────────────────────────────────────────────────────────
            //  Tollgate KeyGen — Admin CLI Tool
            //
            //  Connects to your Tollgate Server and generates / manages keys.
            //  Usage:
            //      tollgate-keygen                          # interactive menu
            //      tollgate-keygen init                     # create/edit tollgate.json
            //      tollgate-keygen generate --app my-app --tier Pro ...
            //      tollgate-keygen help                     # full usage
            //
            //  Config file discovery order (first match wins):
            //    1. $TOLLGATE_CONFIG env var
            //    2. ./tollgate.json                  (current directory)
            //    3. ~/.tollgate/tollgate.json         (user-wide, shared by all apps)
            //    4. %APPDATA%/Tollgate/tollgate.json  (Windows user config dir)
            //       ~/.config/tollgate/tollgate.json  (Linux XDG)
            //
            //  Run `tollgate-keygen init` to create a config file interactively,
            //  then this tool will pick it up automatically on subsequent runs.
            // ─────────────────────────────────────────────────────────────

            // ── Parse args: subcommands ──────────────────────────────────
            if (args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "init":
                    case "configure":
                    case "config":
                        RunInitWizard();
                        return;

                    case "help":
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return;

                    case "generate":
                    case "gen":
                        await RunGenerateOneShotAsync(args[1..]);
                        return;
                }
            }

            // ── Load config from file (or fall back to env vars / prompts) ─
            var cfg = TollgateConfig.Discover();

            string serverUrl;
            string adminKey;

            if (cfg is not null)
            {
                // Found a tollgate.json — use it silently.
                serverUrl = cfg.ServerUrl.TrimEnd('/');
                adminKey = cfg.AdminKey;
                AnsiConsole.MarkupLine($"[grey]Loaded config from:[/] [dim]{TollgateConfig.GetDefaultPath()}[/]");
                AnsiConsole.MarkupLine($"[grey]Server:[/] {serverUrl}  [grey]App:[/] {cfg.AppId}\n");
            }
            else
            {
                // No config file found — prompt + offer to save one
                AnsiConsole.Write(new FigletText("Tollgate").Color(Color.Cyan1));
                AnsiConsole.MarkupLine("[grey]Tollgate Admin Key Generator v1.0[/]\n");
                AnsiConsole.MarkupLine("[yellow]No tollgate.json found.[/]");
                AnsiConsole.MarkupLine("[grey]Run with `init` to create one, OR enter values below for this session only.[/]\n");

                var envServer = Environment.GetEnvironmentVariable("TOLLGATE_SERVER")
                                ?? "http://localhost:7431";
                var envAdmin = Environment.GetEnvironmentVariable("TOLLGATE_ADMIN_KEY")
                                ?? "";

                serverUrl = AnsiConsole.Ask("[cyan]Server URL[/]", envServer).TrimEnd('/');
                adminKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Admin Key[/]")
                        .Secret()
                        .DefaultValue(envAdmin));

                // Offer to save for next time
                if (AnsiConsole.Confirm("\nSave these to a tollgate.json for future use?", true))
                {
                    var savePath = ChooseConfigPath();
                    var newCfg = new TollgateConfig
                    {
                        ServerUrl = serverUrl,
                        AppId = AnsiConsole.Ask("[cyan]Default App ID[/]", "my-app"),
                        AdminKey = adminKey,
                    };
                    newCfg.Save(savePath);
                    AnsiConsole.MarkupLine($"[green]Saved to {savePath}[/]\n");
                    AnsiConsole.MarkupLine("[grey](Add this file to .gitignore — it contains secrets!)[/]\n");
                }
            }

            using var http = new HttpClient();
            var jsonOpts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            if (cfg is null)
            {
                // First-time banner only when we didn't already print it above
                AnsiConsole.Write(new FigletText("Tollgate").Color(Color.Cyan1));
                AnsiConsole.MarkupLine("[grey]Tollgate Admin Key Generator v1.0[/]\n");
            }

            http.BaseAddress = new Uri(serverUrl);

            try
            {
                AnsiConsole.Markup("[grey]Connecting...[/] ");
                var health = await http.GetStringAsync("/api/license/health");
                AnsiConsole.MarkupLine("[green]Connected[/]\n");
            }
            catch
            {
                AnsiConsole.MarkupLine("[red]Cannot connect to server.[/]");
                AnsiConsole.MarkupLine($"[grey]Make sure Tollgate.Server is running at {serverUrl}.[/]");
                return;
            }

            // ── Main menu loop ────────────────────────────────────────────
            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]What would you like to do?[/]")
                        .AddChoices(
                            "Generate license keys",
                            "Add features to an existing key",
                            "List all keys",
                            "Revoke a key",
                            "Reset machine binding",
                            "Register a new app",
                            "List registered apps",
                            "Exit"));

                Console.WriteLine();
                switch (choice)
                {
                    case "Generate license keys": await GenerateKeys(); break;
                    case "Add features to an existing key": await SetFeatures(); break;
                    case "List all keys": await ListKeys(); break;
                    case "Revoke a key": await RevokeKey(); break;
                    case "Reset machine binding": await ResetMachine(); break;
                    case "Register a new app": await RegisterApp(); break;
                    case "List registered apps": await ListApps(); break;
                    case "Exit": return;
                }
                Console.WriteLine();
            }

            // ─────────────────────────────────────────────────────────────
            //  ONE-SHOT GENERATE (scriptable — no interactive prompts)
            //
            //  tollgate-keygen generate --app my-app --tier Pro --count 5
            //      [--days 30] [--features export-pdf,ai] [--notes "..."]
            //      [--out keys.txt] [--server http://...] [--key ADMINKEY]
            // ─────────────────────────────────────────────────────────────
            static async Task RunGenerateOneShotAsync(string[] args)
            {
                string? appId = null, tierArg = null, notes = null, outFile = null;
                string? serverOverride = null, adminOverride = null;
                var features = new List<string>();
                int count = 1;
                int? days = null;

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--app": appId = Next(); break;
                        case "--tier": tierArg = Next(); break;
                        case "--count": int.TryParse(Next(), out count); break;
                        case "--days": if (int.TryParse(Next(), out var d)) days = d; break;
                        case "--features": features = Split(Next()); break;
                        case "--notes": notes = Next(); break;
                        case "--out": outFile = Next(); break;
                        case "--server": serverOverride = Next(); break;
                        case "--key": adminOverride = Next(); break;
                        default:
                            Console.Error.WriteLine($"Unknown option: {args[i]}  (see `tollgate-keygen help`)");
                            Environment.ExitCode = 2;
                            return;
                    }
                    string? Next() => i + 1 < args.Length ? args[++i] : null;
                    List<string> Split(string? csv) =>
                        csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                        ?? new List<string>();
                }

                var cfg = TollgateConfig.Discover();
                var serverUrl = (serverOverride ?? cfg?.ServerUrl ?? "http://localhost:7431").TrimEnd('/');
                var adminKey = adminOverride
                    ?? Environment.GetEnvironmentVariable("TOLLGATE_ADMIN_KEY")
                    ?? cfg?.AdminKey ?? "";

                if (string.IsNullOrWhiteSpace(appId))
                {
                    Console.Error.WriteLine("--app is required. Example: tollgate-keygen generate --app my-app --tier Pro --count 5");
                    Environment.ExitCode = 2;
                    return;
                }
                if (!Enum.TryParse<LicenseTier>(tierArg, ignoreCase: true, out var tier))
                {
                    Console.Error.WriteLine($"--tier is required and must be one of: Free, Basic, Pro, Enterprise, None (trial). Got: {tierArg}");
                    Environment.ExitCode = 2;
                    return;
                }
                if (string.IsNullOrEmpty(adminKey))
                {
                    Console.Error.WriteLine("No admin key: pass --key, set TOLLGATE_ADMIN_KEY, or put adminKey in tollgate.json.");
                    Environment.ExitCode = 2;
                    return;
                }

                var req = new GenerateKeysRequest
                {
                    AppId = appId,
                    Tier = tier,
                    Count = count,
                    ValidDays = days,
                    Features = features,
                    Notes = notes
                };

                using var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/generate")
                {
                    Content = JsonContent.Create(req)
                };
                message.Headers.Add("X-Admin-Key", adminKey);

                try
                {
                    var res = await http.SendAsync(message);
                    var body = await res.Content.ReadFromJsonAsync<GenerateKeysResponse>();
                    if (!res.IsSuccessStatusCode || body is null)
                    {
                        Console.Error.WriteLine($"Failed ({(int)res.StatusCode}): {await res.Content.ReadAsStringAsync()}");
                        Environment.ExitCode = 1;
                        return;
                    }

                    foreach (var key in body.Keys) Console.WriteLine(key);

                    if (!string.IsNullOrEmpty(outFile))
                    {
                        var lines = new List<string>
                        {
                            $"# Tollgate — {tier} Keys",
                            $"# App:        {appId}",
                            $"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                            $"# Expiry:     {(days.HasValue ? days + " days" : "Lifetime")}",
                            $"# Features:   {(features.Count == 0 ? "(none)" : string.Join(", ", features))}",
                            ""
                        };
                        lines.AddRange(body.Keys);
                        File.WriteAllLines(outFile, lines);
                        Console.Error.WriteLine($"Saved {body.Keys.Count} key(s) to {outFile}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    Environment.ExitCode = 1;
                }
            }

            static void PrintUsage()
            {
                Console.WriteLine("""
                Tollgate KeyGen — admin CLI for the Tollgate license server

                USAGE
                  tollgate-keygen                                   interactive menu
                  tollgate-keygen init                              create/edit tollgate.json
                  tollgate-keygen generate [options]                one-shot key generation (CI-friendly)

                GENERATE OPTIONS
                  --app <id>            App ID (required)
                  --tier <t>            Free | Basic | Pro | Enterprise | None (trial)  (required)
                  --count <n>           How many keys (default 1, max 100)
                  --days <n>            Validity window; omit for lifetime keys
                  --features <csv>      Comma-separated feature flags
                  --notes <text>        Internal notes stored with the keys
                  --out <file>          Also write keys to a text file
                  --server <url>        Server URL override
                  --key <adminKey>      Admin key override (else TOLLGATE_ADMIN_KEY / tollgate.json)

                EXAMPLES
                  tollgate-keygen generate --app my-app --tier Pro --count 10 --features export-pdf,ai
                  tollgate-keygen generate --app my-app --tier None --days 30 --out trial-batch.txt

                CONFIG DISCOVERY (first match wins)
                  1. $TOLLGATE_CONFIG
                  2. ./tollgate.json (next to the binary)
                  3. current working directory
                  4. user config dir (per OS)
                """);
            }

            // ─────────────────────────────────────────────────────────────
            //  GENERATE KEYS (interactive)
            // ─────────────────────────────────────────────────────────────
            async Task GenerateKeys()
            {
                var appId = AnsiConsole.Ask("[cyan]App ID[/]", "default");
                var tier = AnsiConsole.Prompt(
                    new SelectionPrompt<LicenseTier>()
                        .Title("[cyan]Tier[/]")
                        .AddChoices(LicenseTier.Free, LicenseTier.Basic, LicenseTier.Pro, LicenseTier.Enterprise, LicenseTier.None));
                var count = AnsiConsole.Ask<int>($"How many [cyan]{tier}[/] keys to generate?", 1);
                var useTrial = AnsiConsole.Confirm("Add expiry (trial license)?", false);
                int? days = useTrial ? AnsiConsole.Ask<int>("Valid for how many days?", 30) : null;

                // Features (optional)
                var features = new List<string>();
                if (AnsiConsole.Confirm("Add explicit features (in addition to the tier)?", false))
                {
                    do
                    {
                        var f = AnsiConsole.Ask<string>("Feature name (blank to finish):", "");
                        if (string.IsNullOrWhiteSpace(f)) break;
                        features.Add(f);
                    } while (true);
                }
                var notes = AnsiConsole.Ask("[grey]Internal notes (optional):[/]", "");

                var req = new GenerateKeysRequest
                {
                    AppId = appId,
                    Tier = tier,
                    Count = count,
                    ValidDays = days,
                    Features = features,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                };

                await AnsiConsole.Status().StartAsync("Generating...", async ctx =>
                {
                    try
                    {
                        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/generate")
                        {
                            Content = JsonContent.Create(req)
                        };
                        message.Headers.Add("X-Admin-Key", adminKey);
                        var res = await http.SendAsync(message);
                        var body = await res.Content.ReadFromJsonAsync<GenerateKeysResponse>(jsonOpts);
                        if (body is null) { AnsiConsole.MarkupLine("[red]Empty response.[/]"); return; }

                        AnsiConsole.MarkupLine($"\n[green]{body.Message}[/]\n");

                        var table = new Table().Border(TableBorder.Rounded)
                            .AddColumn("#").AddColumn("License Key").AddColumn("Tier")
                            .AddColumn("Features").AddColumn("Expiry");
                        for (int i = 0; i < body.Keys.Count; i++)
                        {
                            table.AddRow(
                                $"{i + 1}",
                                $"[yellow]{body.Keys[i]}[/]",
                                tier == LicenseTier.Pro ? "[cyan]Pro[/]"
                                    : tier == LicenseTier.Basic ? "[green]Basic[/]"
                                    : tier.ToString(),
                                features.Count == 0 ? "[grey]—[/]"
                                    : string.Join(", ", features),
                                days.HasValue ? $"{days} days" : "Lifetime");
                        }
                        AnsiConsole.Write(table);

                        if (AnsiConsole.Confirm("\nSave keys to a .txt file?", true))
                        {
                            var filename = $"keys_{appId}_{tier}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                            var lines = new List<string>
                            {
                                $"# Tollgate — {tier} Keys",
                                $"# App:        {appId}",
                                $"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                                $"# Expiry:     {(days.HasValue ? days + " days" : "Lifetime")}",
                                $"# Features:   {(features.Count == 0 ? "(none)" : string.Join(", ", features))}",
                                ""
                            };
                            lines.AddRange(body.Keys);
                            File.WriteAllLines(filename, lines);
                            AnsiConsole.MarkupLine($"[grey]Saved to {filename}[/]");
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                    }
                });
            }

            // ─────────────────────────────────────────────────────────────
            //  SET FEATURES ON AN EXISTING KEY
            // ─────────────────────────────────────────────────────────────
            async Task SetFeatures()
            {
                var appId = AnsiConsole.Ask("[cyan]App ID[/]", "default");
                var key = AnsiConsole.Ask<string>("[cyan]License key[/]:").ToUpperInvariant();

                var features = new List<string>();
                do
                {
                    var f = AnsiConsole.Ask<string>("Feature to add (blank to finish):", "");
                    if (string.IsNullOrWhiteSpace(f)) break;
                    features.Add(f);
                } while (true);

                var payload = new SetFeaturesRequest
                {
                    LicenseKey = key,
                    AppId = appId,
                    Features = features
                };
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/set-features")
                {
                    Content = JsonContent.Create(payload)
                };
                message.Headers.Add("X-Admin-Key", adminKey);
                var res = await http.SendAsync(message);
                if (res.IsSuccessStatusCode)
                    AnsiConsole.MarkupLine("[green]Features updated.[/]");
                else
                    AnsiConsole.MarkupLine($"[red]Failed: {await res.Content.ReadAsStringAsync()}[/]");
            }

            // ─────────────────────────────────────────────────────────────
            //  LIST KEYS (paginated)
            // ─────────────────────────────────────────────────────────────
            async Task ListKeys()
            {
                var appId = AnsiConsole.Ask("[cyan]App ID (blank for all)[/]", "");
                var filter = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Filter by status:")
                        .AddChoices("All", "Active Only", "Revoked Only"));

                string qs = filter switch
                {
                    "Active Only" => "?active=true",
                    "Revoked Only" => "?active=false",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(appId))
                    qs += (qs.Length == 0 ? "?" : "&") + $"appId={Uri.EscapeDataString(appId)}";
                qs += (qs.Length == 0 ? "?" : "&") + "pageSize=500";

                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/keys{qs}");
                    req.Headers.Add("X-Admin-Key", adminKey);
                    var res = await http.SendAsync(req);
                    var page = await res.Content.ReadFromJsonAsync<KeyListResponse>(jsonOpts);
                    var keys = page?.Keys;

                    if (keys is null || keys.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[grey]No keys found.[/]");
                        return;
                    }

                    var table = new Table().Border(TableBorder.Rounded)
                        .AddColumn("Key").AddColumn("App").AddColumn("Tier")
                        .AddColumn("Status").AddColumn("Machine").AddColumn("Features")
                        .AddColumn("Last Seen").AddColumn("Expires");
                    foreach (var k in keys)
                    {
                        var tier = k.Tier == LicenseTier.Pro ? "[cyan]Pro[/]"
                                   : k.Tier == LicenseTier.Basic ? "[green]Basic[/]"
                                   : k.Tier.ToString();
                        var status = k.IsActive ? "[green]Active[/]" : "[red]Revoked[/]";
                        var mid = k.MachineId is null ? "[grey]—[/]"
                                   : k.MachineId[..Math.Min(8, k.MachineId.Length)] + "...";
                        var exp = k.ExpiresAt.HasValue ? k.ExpiresAt.Value.ToString("yyyy-MM-dd") : "Lifetime";
                        var seen = k.LastSeenAt.HasValue ? k.LastSeenAt.Value.ToString("yyyy-MM-dd") : "never";
                        var feat = k.Features.Count == 0 ? "[grey]—[/]" : string.Join(", ", k.Features);
                        table.AddRow($"[yellow]{k.LicenseKey}[/]", k.AppId, tier, status, mid, feat,
                                     seen, exp);
                    }
                    AnsiConsole.MarkupLine(
                        $"[grey]Showing {keys.Count} of {page?.Total ?? keys.Count} key(s) (page {page?.Page ?? 1})[/]\n");
                    AnsiConsole.Write(table);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                }
            }

            // ─────────────────────────────────────────────────────────────
            //  REVOKE / RESET / REGISTER / LIST APPS
            // ─────────────────────────────────────────────────────────────
            async Task RevokeKey()
            {
                var key = AnsiConsole.Ask<string>("License key to revoke:").ToUpperInvariant();
                if (!AnsiConsole.Confirm($"[red]Revoke {key}?[/] This cannot be undone.")) return;
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/revoke")
                {
                    Content = JsonContent.Create(new RevokeKeyRequest { LicenseKey = key })
                };
                message.Headers.Add("X-Admin-Key", adminKey);
                var res = await http.SendAsync(message);
                AnsiConsole.MarkupLine(res.IsSuccessStatusCode ? "[green]Revoked.[/]" : $"[red]{await res.Content.ReadAsStringAsync()}[/]");
            }

            async Task ResetMachine()
            {
                var key = AnsiConsole.Ask<string>("License key to reset:").ToUpperInvariant();
                if (!AnsiConsole.Confirm("Confirm reset?")) return;
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/reset-machine")
                {
                    Content = JsonContent.Create(new ResetMachineRequest { LicenseKey = key })
                };
                message.Headers.Add("X-Admin-Key", adminKey);
                var res = await http.SendAsync(message);
                AnsiConsole.MarkupLine(res.IsSuccessStatusCode ? "[green]Cleared.[/]" : $"[red]{await res.Content.ReadAsStringAsync()}[/]");
            }

            async Task RegisterApp()
            {
                var appId = AnsiConsole.Ask<string>("[cyan]App ID (e.g. my-todo-app)[/]:");
                var name = AnsiConsole.Ask("[cyan]Display name[/]", appId);
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/apps/register")
                {
                    Content = JsonContent.Create(new RegisterAppRequest { AppId = appId, DisplayName = name })
                };
                message.Headers.Add("X-Admin-Key", adminKey);
                var res = await http.SendAsync(message);
                AnsiConsole.MarkupLine(res.IsSuccessStatusCode ? "[green]Registered.[/]" : $"[red]{await res.Content.ReadAsStringAsync()}[/]");
            }

            async Task ListApps()
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/apps");
                req.Headers.Add("X-Admin-Key", adminKey);
                var res = await http.SendAsync(req);
                var apps = await res.Content.ReadFromJsonAsync<List<AppInfo>>(jsonOpts);
                if (apps is null || apps.Count == 0)
                {
                    AnsiConsole.MarkupLine("[grey]No apps registered yet.[/]");
                    return;
                }
                var table = new Table().Border(TableBorder.Rounded)
                    .AddColumn("App ID").AddColumn("Display Name").AddColumn("Keys").AddColumn("Created");
                foreach (var a in apps)
                    table.AddRow($"[yellow]{a.AppId}[/]", a.DisplayName, a.KeyCount.ToString(),
                                 a.CreatedAt.ToString("yyyy-MM-dd"));
                AnsiConsole.Write(table);
            }

            // ─────────────────────────────────────────────────────────────
            //  INIT WIZARD — create or edit tollgate.json
            // ─────────────────────────────────────────────────────────────
            void RunInitWizard()
            {
                AnsiConsole.Write(new FigletText("Tollgate").Color(Color.Cyan1));
                AnsiConsole.MarkupLine("[grey]Tollgate config file wizard[/]\n");

                // Look for an existing config to edit
                var existingPath = TollgateConfig.GetSearchPaths().FirstOrDefault(File.Exists);
                var cfg = existingPath is not null
                    ? TollgateConfig.Load(existingPath) ?? new TollgateConfig()
                    : new TollgateConfig();

                if (existingPath is not null)
                    AnsiConsole.MarkupLine($"[grey]Editing existing config: {existingPath}[/]\n");
                else
                    AnsiConsole.MarkupLine("[grey]Creating a new tollgate.json[/]\n");

                // Prompt for each field with the current value as default
                cfg.ServerUrl = AnsiConsole.Ask("[cyan]Server URL[/]", cfg.ServerUrl);
                cfg.AdminKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Admin Key[/]")
                        .Secret()
                        .DefaultValue(string.IsNullOrEmpty(cfg.AdminKey) ? "" : cfg.AdminKey));
                cfg.AppId = AnsiConsole.Ask("[cyan]Default App ID[/]", cfg.AppId);
                cfg.AppVersion = AnsiConsole.Ask("[cyan]App Version[/]", cfg.AppVersion);
                cfg.PublicKey = AnsiConsole.Ask("[cyan]RSA public key (PEM, blank to skip)[/]", cfg.PublicKey);
                cfg.CacheFile = AnsiConsole.Ask("[cyan]Cache file name[/]", cfg.CacheFile);
                cfg.OfflineGraceDays = AnsiConsole.Ask("[cyan]Offline grace days[/]", cfg.OfflineGraceDays);
                cfg.AllowFreeMode = AnsiConsole.Confirm("Allow free mode?", cfg.AllowFreeMode);

                // Choose where to save
                var savePath = ChooseConfigPath(existingPath);

                cfg.Save(savePath);

                AnsiConsole.MarkupLine($"\n[green]Config saved to: {savePath}[/]");
                AnsiConsole.MarkupLine("[yellow]This file contains secrets — add it to .gitignore![/]");
                AnsiConsole.MarkupLine("[grey]Search order on next run:[/]");
                foreach (var p in TollgateConfig.GetSearchPaths())
                    AnsiConsole.MarkupLine($"  [dim]{(p == savePath ? "→" : " ")} {p}[/]");
            }

            string ChooseConfigPath(string? existing = null)
            {
                if (existing is not null) return existing;

                var options = TollgateConfig.GetSearchPaths()
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select((p, i) => $"{i + 1}. {p}")
                    .ToList();

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Where should the config file be saved?[/]")
                        .AddChoices(options));

                var idx = int.Parse(choice[..1]) - 1;
                return TollgateConfig.GetSearchPaths()[idx];
            }
        }
    }
}
