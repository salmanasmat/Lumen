using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class ServicesTunerViewModel : ObservableObject
{
    private readonly IServicesService _servicesService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    [ObservableProperty]
    private string _title = "Services Tuner";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Load services to optimize Windows background services.";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ServiceItem> _services = new();

    public ServicesTunerViewModel(
        IServicesService servicesService,
        IRestorePointService restorePointService,
        ISessionLogService sessionLogService)
    {
        _servicesService = servicesService;
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    [RelayCommand]
    private async Task LoadServicesAsync()
    {
        IsLoading = true;
        StatusText = "Enumerating Windows services and start configurations...";

        try
        {
            var items = await _servicesService.GetServicesAsync();
            Services = new ObservableCollection<ServiceItem>(items);
            StatusText = $"Loaded {Services.Count} Windows services ({Services.Count(s => s.IsNeverTouch)} protected).";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load services: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplySafePresetAsync()
    {
        IsLoading = true;
        StatusText = "Creating System Restore point before applying Office-Only preset...";

        try
        {
            await _restorePointService.CreateRestorePointAsync("Services Tuner - Apply Office Preset");
            var session = await _sessionLogService.StartSessionAsync("Services Optimization", true);

            var (res, msg) = await _servicesService.ApplySafePresetAsync(session.Id);
            StatusText = msg;
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to apply preset: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DisableServiceAsync(ServiceItem service)
    {
        if (service == null || service.IsNeverTouch) return;

        IsLoading = true;
        try
        {
            var session = await _sessionLogService.StartSessionAsync($"Disable Service '{service.ServiceName}'", true);
            var (res, msg) = await _servicesService.ChangeServiceStartTypeAsync(service, ServiceStartType.Disabled, session.Id);
            StatusText = msg;
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
