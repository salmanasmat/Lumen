using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDiagnosticsService _diagnosticsService;

    [ObservableProperty]
    private string _title = "Diagnostics Scorecard";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private DiagnosticsSnapshot? _snapshot;

    [ObservableProperty]
    private string _statusText = "Ready to scan PC health.";

    public DashboardViewModel(IDiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    [RelayCommand]
    private async Task RunScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning boot logs, WMI storage, RAM/CPU, and system components...";

        try
        {
            Snapshot = await _diagnosticsService.RunFullScanAsync();
            StatusText = $"Scan completed at {Snapshot.Timestamp:t}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
