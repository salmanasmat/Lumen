using System;
using System.Collections.Generic;

namespace Lumen.Core.Models;

public class DiagnosticsSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Boot Time
    public List<double> RecentBootTimesSeconds { get; set; } = new();
    public double AverageBootTimeSeconds { get; set; }
    public MetricSeverity BootTimeSeverity { get; set; }

    // Disks
    public List<DiskMediaInfo> Disks { get; set; } = new();
    public MetricSeverity DiskTypeSeverity { get; set; }
    public double SystemDriveFreeSpaceGb { get; set; }
    public double SystemDriveTotalSpaceGb { get; set; }
    public double SystemDriveFreePercent { get; set; }
    public MetricSeverity FreeSpaceSeverity { get; set; }

    // RAM & CPU
    public double TotalRamGb { get; set; }
    public double AvailableRamGb { get; set; }
    public MetricSeverity RamSeverity { get; set; }
    public string CpuName { get; set; } = string.Empty;
    public int CpuCores { get; set; }

    // System Folders / WinSxS
    public double WinSxSSizeGb { get; set; }
    public double WindowsOldSizeGb { get; set; }
    public bool HasWindowsOld { get; set; }

    // Antivirus & Updates
    public string AntivirusProductName { get; set; } = "Unknown";
    public bool PendingReboot { get; set; }
    public MetricSeverity PendingRebootSeverity { get; set; }
}

public class DiskMediaInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string MediaType { get; set; } = "Unknown"; // SSD / HDD / Unspecified
    public bool IsSsd => MediaType.Equals("SSD", StringComparison.OrdinalIgnoreCase);
}
