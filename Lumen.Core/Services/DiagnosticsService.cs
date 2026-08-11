using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Win32;
using Lumen.Core.Data;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class DiagnosticsService : IDiagnosticsService
{
    private readonly SqliteDataStore _dataStore;

    public DiagnosticsService(SqliteDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task<DiagnosticsSnapshot> RunFullScanAsync()
    {
        var snapshot = new DiagnosticsSnapshot
        {
            Timestamp = DateTime.Now
        };

        // 1. Boot Time History via Event Log
        await Task.Run(() => CollectBootTimes(snapshot));

        // 2. Disk Info & Types (SSD vs HDD)
        await Task.Run(() => CollectDiskMediaTypes(snapshot));

        // 3. System Hardware (RAM & CPU)
        await Task.Run(() => CollectHardwareStats(snapshot));

        // 4. Drive Space
        await Task.Run(() => CollectDriveSpace(snapshot));

        // 5. System Folders (WinSxS & Windows.old)
        await Task.Run(() => CollectSystemFolderSizes(snapshot));

        // 6. Antivirus Status
        await Task.Run(() => CollectAntivirusInfo(snapshot));

        // 7. Windows Update Pending Reboot
        await Task.Run(() => CollectPendingRebootStatus(snapshot));

        // Calculate Severities
        CalculateSeverities(snapshot);

        // Persist to SQLite
        await _dataStore.SaveScanResultAsync(snapshot);

        return snapshot;
    }

    private void CollectBootTimes(DiagnosticsSnapshot snapshot)
    {
        try
        {
            var query = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, "*[System[(EventID=100)]]")
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            int count = 0;

            for (EventRecord eventInstance = reader.ReadEvent(); eventInstance != null && count < 5; eventInstance = reader.ReadEvent())
            {
                using (eventInstance)
                {
                    var xml = eventInstance.ToXml();
                    var bootTime = ExtractBootTimeMsFromXml(xml);
                    if (bootTime > 0)
                    {
                        var seconds = Math.Round(bootTime / 1000.0, 1);
                        snapshot.RecentBootTimesSeconds.Add(seconds);
                        count++;
                    }
                }
            }

            if (snapshot.RecentBootTimesSeconds.Count > 0)
            {
                snapshot.AverageBootTimeSeconds = Math.Round(snapshot.RecentBootTimesSeconds.Average(), 1);
            }
        }
        catch
        {
            // Fallback if event log channel is missing or unreadable
            snapshot.AverageBootTimeSeconds = 0;
        }
    }

    private double ExtractBootTimeMsFromXml(string xml)
    {
        try
        {
            const string target = "name=\"BootTime\">";
            var idx = xml.IndexOf(target, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + target.Length;
                var end = xml.IndexOf("</", start, StringComparison.OrdinalIgnoreCase);
                if (end > start)
                {
                    var valStr = xml.Substring(start, end - start);
                    if (double.TryParse(valStr, out var ms))
                    {
                        return ms;
                    }
                }
            }
        }
        catch
        {
            // Ignore XML parse errors
        }
        return 0;
    }

    private void CollectDiskMediaTypes(DiagnosticsSnapshot snapshot)
    {
        try
        {
            var scope = new ManagementScope(@"root\Microsoft\Windows\Storage");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT DeviceId, Model, MediaType FROM MSFT_PhysicalDisk"));
            foreach (ManagementObject disk in searcher.Get())
            {
                var deviceId = disk["DeviceId"]?.ToString() ?? string.Empty;
                var model = disk["Model"]?.ToString() ?? string.Empty;
                var mediaTypeRaw = Convert.ToInt32(disk["MediaType"] ?? 0);

                string mediaType = mediaTypeRaw switch
                {
                    3 => "HDD",
                    4 => "SSD",
                    5 => "SCM",
                    _ => "Unspecified"
                };

                snapshot.Disks.Add(new DiskMediaInfo
                {
                    DeviceId = deviceId,
                    Model = model.Trim(),
                    MediaType = mediaType
                });
            }
        }
        catch
        {
            // Fallback via Win32_DiskDrive
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT DeviceId, Model, MediaType FROM Win32_DiskDrive");
                foreach (ManagementObject disk in searcher.Get())
                {
                    var model = disk["Model"]?.ToString() ?? string.Empty;
                    var mediaTypeRaw = disk["MediaType"]?.ToString() ?? string.Empty;
                    var mediaType = mediaTypeRaw.Contains("SSD", StringComparison.OrdinalIgnoreCase) || model.Contains("SSD", StringComparison.OrdinalIgnoreCase) || model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ? "SSD" : "HDD";

                    snapshot.Disks.Add(new DiskMediaInfo
                    {
                        DeviceId = disk["DeviceId"]?.ToString() ?? string.Empty,
                        Model = model,
                        MediaType = mediaType
                    });
                }
            }
            catch
            {
                // Ignore fallback failures
            }
        }
    }

    private void CollectHardwareStats(DiagnosticsSnapshot snapshot)
    {
        try
        {
            // Computer System: Total Physical Memory
            using (var sysSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject sys in sysSearcher.Get())
                {
                    if (ulong.TryParse(sys["TotalPhysicalMemory"]?.ToString(), out var bytes))
                    {
                        snapshot.TotalRamGb = Math.Round((double)bytes / (1024 * 1024 * 1024), 2);
                    }
                }
            }

            // Operating System: Free Physical Memory
            using (var osSearcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject os in osSearcher.Get())
                {
                    if (ulong.TryParse(os["FreePhysicalMemory"]?.ToString(), out var kb))
                    {
                        snapshot.AvailableRamGb = Math.Round((double)kb / (1024 * 1024), 2);
                    }
                }
            }

            // CPU Info
            using (var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor"))
            {
                foreach (ManagementObject cpu in cpuSearcher.Get())
                {
                    snapshot.CpuName = cpu["Name"]?.ToString()?.Trim() ?? string.Empty;
                    if (int.TryParse(cpu["NumberOfCores"]?.ToString(), out var cores))
                    {
                        snapshot.CpuCores = cores;
                    }
                    break;
                }
            }
        }
        catch
        {
            // Default fallbacks
        }
    }

    private void CollectDriveSpace(DiagnosticsSnapshot snapshot)
    {
        try
        {
            var systemDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.Equals(@"C:\", StringComparison.OrdinalIgnoreCase)) ?? DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);

            if (systemDrive != null)
            {
                snapshot.SystemDriveTotalSpaceGb = Math.Round((double)systemDrive.TotalSize / (1024 * 1024 * 1024), 2);
                snapshot.SystemDriveFreeSpaceGb = Math.Round((double)systemDrive.AvailableFreeSpace / (1024 * 1024 * 1024), 2);
                if (snapshot.SystemDriveTotalSpaceGb > 0)
                {
                    snapshot.SystemDriveFreePercent = Math.Round((snapshot.SystemDriveFreeSpaceGb / snapshot.SystemDriveTotalSpaceGb) * 100.0, 1);
                }
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }
    }

    private void CollectSystemFolderSizes(DiagnosticsSnapshot snapshot)
    {
        try
        {
            var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var winSxSPath = Path.Combine(windowsPath, "WinSxS");

            if (Directory.Exists(winSxSPath))
            {
                snapshot.WinSxSSizeGb = Math.Round(GetDirectorySize(new DirectoryInfo(winSxSPath)) / (1024.0 * 1024.0 * 1024.0), 2);
            }

            var windowsOldPath = @"C:\Windows.old";
            if (Directory.Exists(windowsOldPath))
            {
                snapshot.HasWindowsOld = true;
                snapshot.WindowsOldSizeGb = Math.Round(GetDirectorySize(new DirectoryInfo(windowsOldPath)) / (1024.0 * 1024.0 * 1024.0), 2);
            }
        }
        catch
        {
            // Ignore access errors on system folders
        }
    }

    private long GetDirectorySize(DirectoryInfo dir)
    {
        long size = 0;
        try
        {
            foreach (var file in dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    size += file.Length;
                }
                catch { }
            }

            foreach (var subDir in dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    size += GetDirectorySize(subDir);
                }
                catch { }
            }
        }
        catch { }
        return size;
    }

    private void CollectAntivirusInfo(DiagnosticsSnapshot snapshot)
    {
        try
        {
            var names = new List<string>();
            var scope = new ManagementScope(@"root\SecurityCenter2");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT displayName FROM AntiVirusProduct"));
            foreach (ManagementObject av in searcher.Get())
            {
                var name = av["displayName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            snapshot.AntivirusProductName = names.Count > 0 ? string.Join(", ", names) : "Windows Defender / Built-in";
        }
        catch
        {
            snapshot.AntivirusProductName = "Windows Defender / Standard";
        }
    }

    private void CollectPendingRebootStatus(DiagnosticsSnapshot snapshot)
    {
        try
        {
            bool rebootRequired = false;

            // Check WU Auto Update RebootRequired
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
            {
                if (key != null) rebootRequired = true;
            }

            // Check Component Based Servicing RebootPending
            if (!rebootRequired)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                {
                    if (key != null) rebootRequired = true;
                }
            }

            snapshot.PendingReboot = rebootRequired;
        }
        catch
        {
            snapshot.PendingReboot = false;
        }
    }

    private void CalculateSeverities(DiagnosticsSnapshot snapshot)
    {
        // 1. Boot Time Severity: Good < 30s, Warning 30-90s, Critical > 90s
        if (snapshot.AverageBootTimeSeconds <= 0)
        {
            snapshot.BootTimeSeverity = MetricSeverity.Good;
        }
        else if (snapshot.AverageBootTimeSeconds < 30)
        {
            snapshot.BootTimeSeverity = MetricSeverity.Good;
        }
        else if (snapshot.AverageBootTimeSeconds <= 90)
        {
            snapshot.BootTimeSeverity = MetricSeverity.Warning;
        }
        else
        {
            snapshot.BootTimeSeverity = MetricSeverity.Critical;
        }

        // 2. Disk Type Severity: HDD = Warning regardless
        var hasHdd = snapshot.Disks.Any(d => d.MediaType.Equals("HDD", StringComparison.OrdinalIgnoreCase));
        snapshot.DiskTypeSeverity = hasHdd ? MetricSeverity.Warning : MetricSeverity.Good;

        // 3. Free Disk Space: Critical < 10% free, Warning < 20% free
        if (snapshot.SystemDriveFreePercent < 10.0)
        {
            snapshot.FreeSpaceSeverity = MetricSeverity.Critical;
        }
        else if (snapshot.SystemDriveFreePercent < 20.0)
        {
            snapshot.FreeSpaceSeverity = MetricSeverity.Warning;
        }
        else
        {
            snapshot.FreeSpaceSeverity = MetricSeverity.Good;
        }

        // 4. RAM Severity: Warning if total < 4GB or available < 2GB, Critical if available < 1GB
        if (snapshot.AvailableRamGb < 1.0)
        {
            snapshot.RamSeverity = MetricSeverity.Critical;
        }
        else if (snapshot.TotalRamGb < 4.0 || snapshot.AvailableRamGb < 2.0)
        {
            snapshot.RamSeverity = MetricSeverity.Warning;
        }
        else
        {
            snapshot.RamSeverity = MetricSeverity.Good;
        }

        // 5. Pending Reboot Severity
        snapshot.PendingRebootSeverity = snapshot.PendingReboot ? MetricSeverity.Warning : MetricSeverity.Good;
    }
}
