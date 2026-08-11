using System;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Lumen.Core.Data;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;
using Task = System.Threading.Tasks.Task;

namespace Lumen.Core.Services;

public class UndoService : IUndoService
{
    private readonly SqliteDataStore _dataStore;

    public UndoService(SqliteDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task<(bool Success, string Message)> UndoActionAsync(ActionRecord action)
    {
        if (!action.IsReversible)
        {
            return (false, "This action is permanent and cannot be undone (e.g. deleted cache files or uninstalled AppX packages).");
        }

        if (action.IsUndone)
        {
            return (false, "This action has already been undone.");
        }

        bool success = false;
        string message = string.Empty;

        try
        {
            switch (action.Module.ToLowerInvariant())
            {
                case "startup":
                    (success, message) = await Task.Run(() => UndoStartupAction(action));
                    break;

                case "services":
                    (success, message) = await Task.Run(() => UndoServiceAction(action));
                    break;

                default:
                    return (false, $"Module '{action.Module}' does not support automated undo.");
            }

            if (success)
            {
                action.IsUndone = true;
                await _dataStore.SaveActionAsync(action);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error reversing action: {ex.Message}");
        }

        return (success, message);
    }

    public async Task<(bool Success, string Message)> UndoSessionAsync(string sessionId)
    {
        var session = (await _dataStore.GetAllSessionsAsync()).Find(s => s.Id == sessionId);
        if (session == null)
        {
            return (false, "Session not found.");
        }

        int undoneCount = 0;
        int failedCount = 0;

        foreach (var action in session.Actions)
        {
            if (action.IsReversible && !action.IsUndone)
            {
                var (res, _) = await UndoActionAsync(action);
                if (res) undoneCount++;
                else failedCount++;
            }
        }

        return (true, $"Session undo completed: {undoneCount} action(s) restored, {failedCount} failed/skipped.");
    }

    private (bool Success, string Message) UndoStartupAction(ActionRecord action)
    {
        try
        {
            if (action.ActionType.Equals("DisableRegistryRun", StringComparison.OrdinalIgnoreCase))
            {
                using var disabledKey = Registry.CurrentUser.OpenSubKey(@"Software\Lumen\DisabledStartup", true);
                if (disabledKey != null)
                {
                    var val = disabledKey.GetValue(action.TargetName)?.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                        if (runKey != null)
                        {
                            runKey.SetValue(action.TargetName, val);
                            disabledKey.DeleteValue(action.TargetName, false);
                            return (true, $"Restored startup registry entry '{action.TargetName}'.");
                        }
                    }
                }
            }
            else if (action.ActionType.Equals("DisableShortcut", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(action.Details))
                {
                    var targetPath = action.Details.Replace(".lumendisabled", ".lnk", StringComparison.OrdinalIgnoreCase);
                    File.Move(action.Details, targetPath, true);
                    return (true, $"Restored startup shortcut '{Path.GetFileName(targetPath)}'.");
                }
            }
            else if (action.ActionType.Equals("DisableScheduledTask", StringComparison.OrdinalIgnoreCase))
            {
                using var ts = new TaskService();
                var schedTask = ts.GetTask(action.TargetName);
                if (schedTask != null)
                {
                    schedTask.Enabled = true;
                    return (true, $"Re-enabled scheduled task '{action.TargetName}'.");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to undo startup item: {ex.Message}");
        }

        return (false, "Could not find backup data for the target startup item.");
    }

    private (bool Success, string Message) UndoServiceAction(ActionRecord action)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(action.BeforeStateJson))
            {
                return (false, "No recorded before-state for this service action.");
            }

            var originalStartTypeInt = JsonSerializer.Deserialize<int>(action.BeforeStateJson);

            using var serviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{action.TargetName}", true);
            if (serviceKey != null)
            {
                serviceKey.SetValue("Start", originalStartTypeInt, RegistryValueKind.DWord);

                if (originalStartTypeInt == 2 || originalStartTypeInt == 3)
                {
                    try
                    {
                        using var sc = new ServiceController(action.TargetName);
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                        }
                    }
                    catch { }
                }

                return (true, $"Restored service '{action.TargetName}' start type to {originalStartTypeInt}.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to restore service start type: {ex.Message}");
        }

        return (false, "Could not access service registry key.");
    }
}
