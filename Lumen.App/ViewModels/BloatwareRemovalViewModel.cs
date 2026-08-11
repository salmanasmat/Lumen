using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class BloatwareRemovalViewModel : ObservableObject
{
    private readonly IBloatwareService _bloatwareService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    [ObservableProperty]
    private string _title = "Bloatware Removal Checklist";

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _statusText = "Load packages to select recommended bloatware removals.";

    [ObservableProperty]
    private ObservableCollection<BloatwarePackage> _packages = new();

    public BloatwareRemovalViewModel(
        IBloatwareService bloatwareService,
        IRestorePointService restorePointService,
        ISessionLogService sessionLogService)
    {
        _bloatwareService = bloatwareService;
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    [RelayCommand]
    private async Task LoadPackagesAsync()
    {
        IsExecuting = true;
        StatusText = "Scanning AppX and provisioned packages...";

        try
        {
            var items = await _bloatwareService.GetInstalledBloatwareAsync();
            Packages = new ObservableCollection<BloatwarePackage>(items);
            StatusText = $"Identified {Packages.Count} removable/bloatware package patterns.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to scan packages: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var pkg in Packages)
        {
            if (!pkg.IsProtected)
            {
                pkg.IsSelected = true;
            }
        }
        StatusText = $"Selected {Packages.Count(p => p.IsSelected)} recommended packages for removal.";
    }

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        var selected = Packages.Where(p => p.IsSelected && !p.IsProtected).ToList();
        if (!selected.Any())
        {
            StatusText = "No non-protected packages selected for removal.";
            return;
        }

        IsExecuting = true;
        StatusText = $"Creating System Restore point before removing {selected.Count} package(s)...";

        try
        {
            await _restorePointService.CreateRestorePointAsync("AppX Bloatware Removal");
            var session = await _sessionLogService.StartSessionAsync("Bloatware Purge", true);

            int removedCount = 0;
            foreach (var pkg in selected)
            {
                StatusText = $"Removing '{pkg.DisplayName}' ({removedCount + 1}/{selected.Count})...";
                var (res, msg) = await _bloatwareService.RemovePackageAsync(pkg, session.Id);
                if (res) removedCount++;
            }

            StatusText = $"Successfully removed {removedCount} bloatware package(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Removal failed: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
            await LoadPackagesAsync();
        }
    }
}
