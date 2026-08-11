using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ISessionLogService _sessionLogService;
    private readonly IUndoService _undoService;

    [ObservableProperty]
    private string _title = "History & Session Logs";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Browse past optimization sessions and system scans.";

    [ObservableProperty]
    private ObservableCollection<SessionRecord> _sessions = new();

    public HistoryViewModel(ISessionLogService sessionLogService, IUndoService undoService)
    {
        _sessionLogService = sessionLogService;
        _undoService = undoService;
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        IsLoading = true;
        StatusText = "Loading session history from local database...";

        try
        {
            var items = await _sessionLogService.GetAllSessionsAsync();
            Sessions = new ObservableCollection<SessionRecord>(items);
            StatusText = $"Loaded {Sessions.Count} past optimization session(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load history: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UndoActionAsync(ActionRecord action)
    {
        if (action == null || !action.IsReversible || action.IsUndone) return;

        IsLoading = true;
        try
        {
            var (res, msg) = await _undoService.UndoActionAsync(action);
            StatusText = msg;
        }
        catch (Exception ex)
        {
            StatusText = $"Undo failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task UndoSessionAsync(SessionRecord session)
    {
        if (session == null) return;

        IsLoading = true;
        try
        {
            var (res, msg) = await _undoService.UndoSessionAsync(session.Id);
            StatusText = msg;
        }
        catch (Exception ex)
        {
            StatusText = $"Session undo failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            await LoadHistoryAsync();
        }
    }
}
