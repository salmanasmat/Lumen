using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class DiskCleanupViewModel : ObservableObject
{
    private readonly IDiskCleanupService _diskCleanupService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    [ObservableProperty]
    private string _title = "Disk Cleanup";

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private string _statusText = "Calculate reclaimable space to review cleanups.";

    [ObservableProperty]
    private ObservableCollection<CleanupCategoryItem> _categories = new();

    [ObservableProperty]
    private double _totalSelectedMb;

    public DiskCleanupViewModel(
        IDiskCleanupService diskCleanupService,
        IRestorePointService restorePointService,
        ISessionLogService sessionLogService)
    {
        _diskCleanupService = diskCleanupService;
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    [RelayCommand]
    private async Task CalculateSizesAsync()
    {
        IsCalculating = true;
        StatusText = "Calculating reclaimable space across drive categories...";

        try
        {
            var items = await _diskCleanupService.CalculateReclaimableSizesAsync();
            Categories = new ObservableCollection<CleanupCategoryItem>(items);
            UpdateTotalSelected();
            StatusText = $"Calculated ~{Math.Round(Categories.Sum(c => c.SizeMb) / 1024, 2)} GB total reclaimable space.";
        }
        catch (Exception ex)
        {
            StatusText = $"Calculation failed: {ex.Message}";
        }
        finally
        {
            IsCalculating = false;
        }
    }

    [RelayCommand]
    private void SelectSafeCleanPreset()
    {
        foreach (var cat in Categories)
        {
            cat.IsSelected = cat.IsSafePreset;
        }
        UpdateTotalSelected();
        StatusText = "Selected 'Safe Clean' preset (Temp files, Chrome cache, WER logs, Recycle Bin).";
    }

    [RelayCommand]
    private async Task ExecuteCleanupAsync()
    {
        var selected = Categories.Where(c => c.IsSelected).ToList();
        if (!selected.Any())
        {
            StatusText = "No cleanup categories selected.";
            return;
        }

        IsCalculating = true;
        StatusText = $"Executing disk cleanup for {selected.Count} category/categories...";

        try
        {
            await _restorePointService.CreateRestorePointAsync("Disk Cleanup Execution");
            var session = await _sessionLogService.StartSessionAsync("Disk Cleanup", true);

            var (res, msg) = await _diskCleanupService.ExecuteCleanupAsync(selected, session.Id);
            StatusText = msg;
        }
        catch (Exception ex)
        {
            StatusText = $"Disk cleanup failed: {ex.Message}";
        }
        finally
        {
            IsCalculating = false;
            await CalculateSizesAsync();
        }
    }

    public void UpdateTotalSelected()
    {
        TotalSelectedMb = Math.Round(Categories.Where(c => c.IsSelected).Sum(c => c.SizeMb), 1);
    }
}
