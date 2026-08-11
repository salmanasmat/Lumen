using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class StartupManagerViewModel : ObservableObject
{
    private readonly IStartupService _startupService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    [ObservableProperty]
    private string _title = "Startup Manager";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Load startup items to review boot impact.";

    [ObservableProperty]
    private ObservableCollection<StartupEntry> _entries = new();

    public StartupManagerViewModel(
        IStartupService startupService,
        IRestorePointService restorePointService,
        ISessionLogService sessionLogService)
    {
        _startupService = startupService;
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    [RelayCommand]
    private async Task LoadEntriesAsync()
    {
        IsLoading = true;
        StatusText = "Scanning registry, startup folders, and scheduled tasks...";

        try
        {
            var items = await _startupService.GetStartupEntriesAsync();
            Entries = new ObservableCollection<StartupEntry>(items);
            StatusText = $"Loaded {Entries.Count} startup items ({Entries.Count(e => e.Risk == RiskLevel.Safe)} safe to disable).";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load startup items: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleEntryAsync(StartupEntry entry)
    {
        if (entry == null) return;

        IsLoading = true;
        try
        {
            // Ensure restore point session exists
            var session = await _sessionLogService.StartSessionAsync($"Toggle Startup Entry '{entry.Name}'", true);

            if (entry.IsEnabled)
            {
                var (res, msg) = await _startupService.DisableEntryAsync(entry, session.Id);
                StatusText = msg;
            }
            else
            {
                var (res, msg) = await _startupService.EnableEntryAsync(entry, session.Id);
                StatusText = msg;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error toggling '{entry.Name}': {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            await LoadEntriesAsync();
        }
    }

    [RelayCommand]
    private async Task DisableAllSafeAsync()
    {
        var safeEntries = Entries.Where(e => e.Risk == RiskLevel.Safe && e.IsEnabled).ToList();
        if (!safeEntries.Any())
        {
            StatusText = "No active safe-to-disable startup items found.";
            return;
        }

        IsLoading = true;
        StatusText = $"Disabling {safeEntries.Count} low-risk startup item(s)...";

        try
        {
            await _restorePointService.CreateRestorePointAsync("Disable All Safe Startup Items");
            var session = await _sessionLogService.StartSessionAsync("Bulk Disable Safe Startup Items", true);

            int disabledCount = 0;
            foreach (var entry in safeEntries)
            {
                var (res, _) = await _startupService.DisableEntryAsync(entry, session.Id);
                if (res) disabledCount++;
            }

            StatusText = $"Disabled {disabledCount} low-risk startup item(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Bulk disable failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            await LoadEntriesAsync();
        }
    }
}
