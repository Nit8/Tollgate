using System.Reflection;
using Tollgate.Abstractions;
using Tollgate.Licensing;
using Tollgate.Samples.ConsoleApp.TodoApp;

namespace Tollgate.Samples.ConsoleApp
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // ?????????????????????????????????????????????????????????????
            //  Tollgate Sample — Console TODO app
            //
            //  Demonstrates the simplest possible usage:
            //
            //    1. Configure the client with your Tollgate server URL
            //    2. Try to load saved license (offline-first)
            //    3. If no license, prompt for activation
            //    4. Run the app — every gated method throws
            //       LicenseRequiredException if the user lacks access
            // ?????????????????????????????????????????????????????????????

            // ?? STEP 1 — configure ????????????????????????????????????????
            LicenseGate.Configure(o =>
            {
                o.ServerUrl = "http://localhost:5000";
                o.AppId = "sample-todo-console";
                o.AppVersion = "1.0.0";
                o.CacheFile = "license.dat";
                o.OfflineGraceDays = 7;
            });

            Console.WriteLine("== Tollgate Sample: TODO app ==");
            Console.WriteLine($"Machine ID: {MachineFingerprint.Get()}\n");

            // ?? STEP 2 — load saved license ????????????????????????????????
            var loaded = await LicenseGate.TryLoadSavedLicenseAsync();

            // ?? STEP 3 — activate if needed ?????????????????????????????????
            if (!loaded)
            {
                Console.WriteLine("No license found.");
                Console.Write("Enter license key (blank to use free mode): ");
                var key = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    var result = await LicenseGate.ActivateKeyAsync(key);
                    Console.WriteLine(result.IsValid
                        ? $"? {result.Message}  Tier={result.Tier}, Features=[{string.Join(", ", result.Features)}]"
                        : $"? {result.Message}");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("Running in free mode. Type 'help' for commands.\n");
                }
            }

            PrintStatus();

            // ?? STEP 4 — REPL ??????????????????????????????????????????????
            var svc = new TodoService();
            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToLowerInvariant();
                var arg = parts.Length > 1 ? parts[1] : "";

                try
                {
                    switch (cmd)
                    {
                        case "list":
                            var items = svc.List();
                            if (items.Count == 0) Console.WriteLine("(empty)");
                            else for (int i = 0; i < items.Count; i++)
                                Console.WriteLine($"  [{i}] {items[i]}");
                            break;

                        case "add":
                            RunGated(nameof(TodoService.Add), arg);
                            break;

                        case "del":
                            if (int.TryParse(arg, out var idx)) RunGated(nameof(TodoService.Delete), idx);
                            else Console.WriteLine("Usage: del <index>");
                            break;

                        case "bulk":
                            // demo: bulk-import 5 sample items.
                            // (object) cast forces the string[] to be a SINGLE element
                            // of the params array, not the params array itself.
                            RunGated(nameof(TodoService.BulkImport),
                                (object)new[] { "Task A", "Task B", "Task C", "Task D", "Task E" });
                            Console.WriteLine("? Imported 5 tasks.");
                            break;

                        case "pdf":
                            RunGated(nameof(TodoService.ExportToPdf));
                            break;

                        case "ai":
                            RunGated(nameof(TodoService.SuggestNextTask));
                            break;

                        case "status":
                            PrintStatus();
                            break;

                        case "activate":
                            Console.Write("Enter license key: ");
                            var k = Console.ReadLine()?.Trim();
                            if (!string.IsNullOrEmpty(k))
                            {
                                var r = await LicenseGate.ActivateKeyAsync(k);
                                Console.WriteLine(r.IsValid ? $"? {r.Message}" : $"? {r.Message}");
                                PrintStatus();
                            }
                            break;

                        case "deactivate":
                            LicenseGate.ClearLicense();
                            Console.WriteLine("? License cleared.");
                            PrintStatus();
                            break;

                        case "help":
                            Console.WriteLine("Commands:");
                            Console.WriteLine("  list            show todos (free)");
                            Console.WriteLine("  add <text>      add todo  [Basic]");
                            Console.WriteLine("  del <i>         delete    [Basic]");
                            Console.WriteLine("  bulk            import 5  [Pro]");
                            Console.WriteLine("  pdf             export    [Feature: export-pdf]");
                            Console.WriteLine("  ai              suggest   [Feature: ai-assist]");
                            Console.WriteLine("  status          show license state");
                            Console.WriteLine("  activate        enter license key");
                            Console.WriteLine("  deactivate      clear license");
                            Console.WriteLine("  exit");
                            break;

                        case "exit":
                            return;

                        default:
                            Console.WriteLine($"Unknown: {cmd}. Type 'help'.");
                            break;
                    }
                }
                catch (LicenseRequiredException ex)
                {
                    Console.WriteLine($"?? {ex.Message}");
                }
            }

            // ?? Helper: invoke a TodoService method by name, enforcing
            //    [RequireFeature] / [RequireTier] attributes via reflection.
            //    This is exactly what Tollgate.AspNetCore's RequireFeatureFilter
            //    does automatically for controllers / actions.
            void RunGated(string methodName, params object?[] args)
            {
                var method = typeof(TodoService).GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (method is null)
                    throw new InvalidOperationException($"Method not found: {methodName}");

                // Throws LicenseRequiredException if attributes aren't satisfied.
                LicenseGate.EnsureAccessFor(method);

                // Invoke — handle the result specially for display.
                var result = method.Invoke(svc, args);
                if (result is byte[] bytes)
                    Console.WriteLine($"? Exported {bytes.Length} bytes (placeholder).");
                else if (result is string s)
                    Console.WriteLine($"? Suggestion: {s}");
            }

            void PrintStatus()
            {
                var s = LicenseGate.Current;
                if (!s.IsValid)
                    Console.WriteLine($"\n  License:  [FREE MODE]  Tier=None\n");
                else
                    Console.WriteLine(
                        $"\n  License:  ?  Tier={s.Tier}  " +
                        $"Features=[{string.Join(", ", s.Features)}]  " +
                        $"Key={s.LicenseKey}  " +
                        $"Expiry={(s.ExpiresAt?.ToString("yyyy-MM-dd") ?? "Lifetime")}\n");
            }

        }
    }
}
