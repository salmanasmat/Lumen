using System.Collections.Generic;

namespace Lumen.Core.Models;

public class NetworkDiagnosticResult
{
    public string ServerTarget { get; set; } = string.Empty;
    public double DnsResolutionTimeMs { get; set; }
    public bool DnsResolved { get; set; }
    
    public double PingAverageLatencyMs { get; set; }
    public int PingPacketLossPercent { get; set; }
    public bool PingSuccess { get; set; }

    public List<MappedDriveInfo> MappedDrives { get; set; } = new();
    public bool HasLogonDelayWarning { get; set; }
}

public class MappedDriveInfo
{
    public string DriveLetter { get; set; } = string.Empty; // e.g. "Z:"
    public string RemotePath { get; set; } = string.Empty;
    public bool ReconnectAtLogon { get; set; }
    public bool IsReachable { get; set; }
    public double ResponseTimeMs { get; set; }
    public bool IsCausingLogonDelay => ReconnectAtLogon && !IsReachable;
}
