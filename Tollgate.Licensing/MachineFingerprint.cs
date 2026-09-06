using System.Security.Cryptography;
using System.Text;

namespace Tollgate.Licensing
{
    // ─────────────────────────────────────────────────────────────
    //  MACHINE FINGERPRINT
    //
    //  Cross-platform: works on Windows, Linux and macOS.
    //  Windows  → uses WMI (CPU + Disk + Motherboard serials)
    //  Linux    → uses /etc/machine-id (or /var/lib/dbus/machine-id)
    //  macOS    → uses `ioreg` for IOPlatformUUID
    //  Falls back to Environment.MachineName + UserName hash.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a stable, anonymous machine fingerprint.
    /// Same hardware → same fingerprint, even after reboots or
    /// reinstalls. No personally identifying information is sent.
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
        try
        {
            string cpu   = WmiValue("Win32_Processor",  "ProcessorId");
            string disk  = WmiValue("Win32_DiskDrive",  "SerialNumber");
            string board = WmiValue("Win32_BaseBoard",  "SerialNumber");
            if (string.IsNullOrEmpty(cpu) && string.IsNullOrEmpty(disk)) return null;
            return $"win|{cpu}|{disk}|{board}";
        }
        catch { return null; }
#else
            return null;
#endif
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
                    UseShellExecute = false
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p is null) return null;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);
                var match = System.Text.RegularExpressions.Regex.Match(
                    output, @"""IOPlatformUUID""\s*=\s*""([^""]+)""");
                if (match.Success)
                    return $"macos|{match.Groups[1].Value}";
            }
            catch { }
            return null;
        }

        // ── Fallback ──────────────────────────────────────────────
        private static string FallbackFingerprint()
        {
            var raw = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return "FB" + Convert.ToHexString(hash)[..14];
        }
    }

}