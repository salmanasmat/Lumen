using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using Lumen.Core.Interfaces;

namespace Lumen.Core.Services;

public class RestorePointService : IRestorePointService
{
    public Task<bool> IsSystemRestoreEnabledAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var scope = new ManagementScope(@"root\default");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM SystemRestoreConfig"));
                foreach (ManagementObject config in searcher.Get())
                {
                    // If any drive is enabled for system restore
                    return true;
                }
            }
            catch
            {
                // Fallback check via PowerShell or registry
            }

            try
            {
                // PowerShell check if Checkpoint-Computer is available
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-ComputerRestorePoint).Count\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(3000);
                    return proc.ExitCode == 0;
                }
            }
            catch { }

            return true; // Assume true or allow attempt
        });
    }

    public Task<(bool Success, string Message)> CreateRestorePointAsync(string description)
    {
        return Task.Run(() =>
        {
            try
            {
                var safeDescription = description.Replace("\"", "'");
                var script = $"Checkpoint-Computer -Description \"Lumen: {safeDescription}\" -RestorePointType \"MODIFY_SETTINGS\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var stdout = proc.StandardOutput.ReadToEnd();
                    var stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(15000);

                    if (proc.ExitCode == 0)
                    {
                        return (true, "System Restore point created successfully.");
                    }
                    else
                    {
                        return (false, $"System Restore point creation returned error: {stderr}");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to invoke Checkpoint-Computer: {ex.Message}");
            }

            return (false, "Could not start PowerShell process to create System Restore point.");
        });
    }
}
