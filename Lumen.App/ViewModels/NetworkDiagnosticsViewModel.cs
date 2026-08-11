using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class NetworkDiagnosticsViewModel : ObservableObject
{
    private readonly INetworkDiagnosticsService _networkDiagnosticsService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    [ObservableProperty]
    private string _title = "Network & Logon Diagnostics";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Enter server target or run mapped drive diagnostics.";

    [ObservableProperty]
    private string _serverTarget = string.Empty;

    [ObservableProperty]
    private NetworkDiagnosticResult? _result;

    public NetworkDiagnosticsViewModel(
        INetworkDiagnosticsService networkDiagnosticsService,
        IRestorePointService restorePointService,
        ISessionLogService sessionLogService)
    {
        _networkDiagnosticsService = networkDiagnosticsService;
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        IsRunning = true;
        StatusText = "Measuring DNS resolution, ping latency, and testing mapped drive reachability...";

        try
        {
            Result = await _networkDiagnosticsService.RunNetworkDiagnosticsAsync(ServerTarget);
            if (Result.HasLogonDelayWarning)
            {
                StatusText = "WARNING: Unreachable mapped drive(s) set to reconnect at logon detected! This adds delay to every logon.";
            }
            else
            {
                StatusText = "Network & logon diagnostics completed cleanly.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Diagnostics failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task DisableReconnectAsync(MappedDriveInfo drive)
    {
        if (drive == null) return;

        IsRunning = true;
        try
        {
            var session = await _sessionLogService.StartSessionAsync($"Disable Reconnect at Logon for Drive {drive.DriveLetter}", true);
            var (res, msg) = await _networkDiagnosticsService.DisableDriveReconnectAsync(drive.DriveLetter, session.Id);
            StatusText = msg;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            await RunDiagnosticsAsync();
        }
    }
}
