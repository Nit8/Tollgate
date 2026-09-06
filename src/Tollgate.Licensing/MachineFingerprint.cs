using System.Security.Cryptography;
using System.Text;

namespace Tollgate.Licensing
{
    // ─────────────────────────────────────────────────────────────
    //  MACHINE FINGERPRINT
    //
    //  Cross-platform: works on Windows, Linux and macOS.
    //  Windows (net10.0-windows TFM) → WMI (CPU + Disk + Motherboard serials)
    //  Windows (plain TFM)           → registry MachineGuid (stable per OS
    //                                  install, no WMI dependency)
    //  Linux    → /etc/machine-id (or /var/lib/dbus/machine-id)
    //  macOS    → `ioreg` for IOPlatformUUID
    //  Falls back to a MachineName + OSVersion hash.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a stable, anonymous machine fingerprint.
    /// Same hardware → same fingerprint, even after reboots.
    /// No personally identifying information (user name is never included).
    /// </summary>
    public static class MachineFingerprint
    {
        private static string? _cached;

        /// <summary>Returns a 16-character hex machine ID.</summary>
        public static string Get()
        {
            if (_cached is not null) return _cached;

            try
            {
                var raw = TryGetWindowsFingerprint()
                          ?? TryGetLinuxFingerprint()
                          ?? TryGetMacOsFingerprint()
                          ?? FallbackFingerprint();

                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
                _cached = Convert.ToHexString(hash)[..16];
            }
            catch
            {
                _cached = FallbackFingerprint();
            }

            return _cached;
        }

        // ── Windows ────────────────────────────────────────────────
        private static string? TryGetWindowsFingerprint()
        {
            if (!OperatingSystem.IsWindows()) return null;

#if NET10_0_WINDOWS
            // WMI path — hardware serials (CPU, disk, motherboard). Compiled
            // only into the net10.0-windows asset of the package.
            try
            {
                string cpu   = WmiValue("Win32_Processor", "ProcessorId");
                string disk  = WmiValue("Win32_DiskDrive", "SerialNumber");
                string board = WmiValue("Win32_BaseBoard", "SerialNumber");
                if (string.IsNullOrEmpty(cpu) && string.IsNullOrEmpty(disk)) return null;
                return $"win|{cpu}|{disk}|{board}";
            }
            catch { /* fall through to the registry path */ }
#endif
            // Registry path — MachineGuid is stable for the lifetime of the
            // OS install and available from every TFM without WMI. Used when
            // the app targets plain net10.0/net8.0 (the common case for
            // consumers referencing the NuGet package on Windows).
            try
            {
                var guid = ReadMachineGuid();
                if (!string.IsNullOrWhiteSpace(guid)) return $"win|{guid}";
            }
            catch { /* fall through to platform probes */ }
            return null;
        }

#if NET10_0_WINDOWS
        private static string WmiValue(string cls, string prop)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT {prop} FROM {cls}");
                foreach (var obj in searcher.Get())
                    return obj[prop]?.ToString()?.Trim() ?? "";
            }
            catch { }
            return "";
        }
#endif

        /// <summary>
        /// Read HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid, checking
        /// both 64-bit and 32-bit views.
        /// </summary>
        private static string? ReadMachineGuid()
        {
            const string subKey = @"SOFTWARE\Microsoft\Cryptography";

            using var view64 = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
            var value = view64.OpenSubKey(subKey)?.GetValue("MachineGuid") as string;
            if (!string.IsNullOrWhiteSpace(value)) return value;

            using var view32 = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32);
            return view32.OpenSubKey(subKey)?.GetValue("MachineGuid") as string;
        }

        // ── Linux ─────────────────────────────────────────────────
        private static string? TryGetLinuxFingerprint()
        {
            if (!OperatingSystem.IsLinux()) return null;
            try
            {
                foreach (var p in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
                    if (File.Exists(p))
                        return $"linux|{File.ReadAllText(p).Trim()}";
            }
            catch { }
            return null;
        }

        // ── macOS ─────────────────────────────────────────────────
        private static string? TryGetMacOsFingerprint()
        {
            if (!OperatingSystem.IsMacOS()) return null;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/sbin/ioreg",
                    Arguments = "-rd1 -c IOPlatformExpertDevice",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p is null) return null;

                // Read asynchronously so a large output stream cannot
                // deadlock the pipe, and enforce the timeout on the wait.
                var outputTask = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(2000)) return null;
                var output = outputTask.IsCompleted ? outputTask.Result : "";

                var match = System.Text.RegularExpressions.Regex.Match(
                    output, "\"IOPlatformUUID\"\\s*=\\s*\"([^\"]+)\"");
                if (match.Success)
                    return $"macos|{match.Groups[1].Value}";
            }
            catch { }
            return null;
        }

        // ── Fallback ──────────────────────────────────────────────
        // Deliberately excludes Environment.UserName (PII, and it changes
        // when the user creates a new account on the same machine).
        private static string FallbackFingerprint()
        {
            var raw = $"fallback|{Environment.MachineName}|{Environment.OSVersion}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return "FB" + Convert.ToHexString(hash)[..14];
        }
    }
}
