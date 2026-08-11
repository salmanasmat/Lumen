using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Win32;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class NetworkDiagnosticsService : INetworkDiagnosticsService
{
    private readonly ISessionLogService _sessionLogService;

    public NetworkDiagnosticsService(ISessionLogService sessionLogService)
    {
        _sessionLogService = sessionLogService;
    }

    public async Task<NetworkDiagnosticResult> RunNetworkDiagnosticsAsync(string serverTarget)
    {
        return await Task.Run(() =>
        {
            var result = new NetworkDiagnosticResult
            {
                ServerTarget = serverTarget
            };

            // 1. DNS Resolution Time
            if (!string.IsNullOrWhiteSpace(serverTarget))
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var entry = Dns.GetHostEntry(serverTarget);
                    sw.Stop();
                    result.DnsResolutionTimeMs = sw.ElapsedMilliseconds;
                    result.DnsResolved = entry.AddressList.Length > 0;
                }
                catch
                {
                    sw.Stop();
                    result.DnsResolutionTimeMs = sw.ElapsedMilliseconds;
                    result.DnsResolved = false;
                }

                // 2. Ping Latency & Packet Loss (4 attempts)
                try
                {
                    using var pinger = new Ping();
                    long totalMs = 0;
                    int successfulPings = 0;

                    for (int i = 0; i < 4; i++)
                    {
                        var reply = pinger.Send(serverTarget, 2000);
                        if (reply.Status == IPStatus.Success)
                        {
                            totalMs += reply.RoundtripTime;
                            successfulPings++;
                        }
                    }

                    if (successfulPings > 0)
                    {
                        result.PingAverageLatencyMs = Math.Round((double)totalMs / successfulPings, 1);
                        result.PingSuccess = true;
                    }
                    result.PingPacketLossPercent = (int)Math.Round(((4 - successfulPings) / 4.0) * 100.0);
                }
                catch
                {
                    result.PingSuccess = false;
                    result.PingPacketLossPercent = 100;
                }
            }

            // 3. Mapped Network Drives (including Z:\ monitoring)
            CollectMappedDrives(result);

            return result;
        });
    }

    public async Task<(bool Success, string Message)> DisableDriveReconnectAsync(string driveLetter, string sessionId)
    {
        return await Task.Run(async () =>
        {
            try
            {
                // Remove trailing colon if passed as "Z:" -> "Z"
                var letter = driveLetter.TrimEnd(':').ToUpperInvariant();
                using var key = Registry.CurrentUser.OpenSubKey(@$"Network\{letter}", true);
                if (key != null)
                {
                    // Reconnect flag is stored in DWORD ConnectionType or SaveConnection (0 = No reconnect)
                    key.SetValue("SaveConnection", 0, RegistryValueKind.DWord);

                    await _sessionLogService.LogActionAsync(new ActionRecord
                    {
                        SessionId = sessionId,
                        Module = "Network",
                        ActionType = "DisableDriveReconnect",
                        TargetName = $"{letter}:",
                        Details = "Disabled reconnect at logon to eliminate logon delay",
                        BeforeStateJson = "1",
                        IsReversible = true,
                        IsUndone = false
                    });

                    return (true, $"Disabled reconnect-at-logon for drive {letter}:.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to modify drive reconnect setting: {ex.Message}");
            }

            return (false, $"Drive letter mapping for {driveLetter} not found in HKCU\\Network.");
        });
    }

    private void CollectMappedDrives(NetworkDiagnosticResult result)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, ProviderName, ConnectionType FROM Win32_MappedLogicalDisk");
            foreach (ManagementObject disk in searcher.Get())
            {
                var letter = disk["DeviceID"]?.ToString() ?? string.Empty;
                var provider = disk["ProviderName"]?.ToString() ?? string.Empty;

                bool reconnectAtLogon = CheckReconnectRegistryFlag(letter);
                bool isReachable = CheckPathReachability(letter);

                var driveInfo = new MappedDriveInfo
                {
                    DriveLetter = letter,
                    RemotePath = provider,
                    ReconnectAtLogon = reconnectAtLogon,
                    IsReachable = isReachable
                };

                result.MappedDrives.Add(driveInfo);

                if (driveInfo.IsCausingLogonDelay)
                {
                    result.HasLogonDelayWarning = true;
                }
            }
        }
        catch
        {
            // Registry fallback check if WMI Win32_MappedLogicalDisk unavailable
            try
            {
                using var networkKey = Registry.CurrentUser.OpenSubKey("Network");
                if (networkKey != null)
                {
                    foreach (var letter in networkKey.GetSubKeyNames())
                    {
                        using var driveKey = networkKey.OpenSubKey(letter);
                        if (driveKey != null)
                        {
                            var provider = driveKey.GetValue("RemotePath")?.ToString() ?? string.Empty;
                            bool isReachable = CheckPathReachability($"{letter}:");

                            var driveInfo = new MappedDriveInfo
                            {
                                DriveLetter = $"{letter}:",
                                RemotePath = provider,
                                ReconnectAtLogon = true,
                                IsReachable = isReachable
                            };

                            result.MappedDrives.Add(driveInfo);

                            if (driveInfo.IsCausingLogonDelay)
                            {
                                result.HasLogonDelayWarning = true;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    private bool CheckReconnectRegistryFlag(string driveLetter)
    {
        try
        {
            var letter = driveLetter.TrimEnd(':').ToUpperInvariant();
            using var key = Registry.CurrentUser.OpenSubKey(@$"Network\{letter}");
            if (key != null)
            {
                var saveConn = Convert.ToInt32(key.GetValue("SaveConnection") ?? 1);
                return saveConn == 1;
            }
        }
        catch { }
        return true;
    }

    private bool CheckPathReachability(string driveLetterOrPath)
    {
        try
        {
            var task = Task.Run(() => Directory.Exists(driveLetterOrPath));
            if (task.Wait(3000))
            {
                return task.Result;
            }
        }
        catch { }
        return false;
    }
}
