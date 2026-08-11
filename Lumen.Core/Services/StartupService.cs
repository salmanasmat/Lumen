using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;
using Task = System.Threading.Tasks.Task;

namespace Lumen.Core.Services;

public class StartupService : IStartupService
{
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    private static readonly Dictionary<string, RiskLevel> CuratedRiskDictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Adobe", RiskLevel.Safe },
        { "Adobe Updater", RiskLevel.Safe },
        { "Spotify", RiskLevel.Safe },
        { "Steam", RiskLevel.Caution },
        { "OneDrive", RiskLevel.Caution },
        { "Dropbox", RiskLevel.Caution },
        { "Cortana", RiskLevel.Safe },
        { "Skype", RiskLevel.Safe },
        { "Discord", RiskLevel.Caution },
        { "EpicGamesLauncher", RiskLevel.Caution },
        { "iTunes", RiskLevel.Safe },
        { "CCleaner", RiskLevel.Caution },
        { "uTorrent", RiskLevel.Caution },
        { "BitTorrent", RiskLevel.Caution },
        { "GoogleDrive", RiskLevel.Caution },
        { "Evernote", RiskLevel.Safe },
        { "Slack", RiskLevel.Caution },
        { "Teams", RiskLevel.Caution },
        { "Zoom", RiskLevel.Caution },
        { "Cisco Webex", RiskLevel.Caution }
    };

    public StartupService(IRestorePointService restorePointService, ISessionLogService sessionLogService)
    {
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    public async Task<List<StartupEntry>> GetStartupEntriesAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<StartupEntry>();

            // 1. Registry Run HKCU
            ReadRegistryRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRunUser, list);

            // 2. Registry Run HKLM (64-bit and 32-bit)
            ReadRegistryRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRunMachine, list);
            ReadRegistryRunKey(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRunMachine, list);

            // 3. Startup Folder Shortcuts
            ReadStartupFolderShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.StartupFolderUser, list);
            ReadStartupFolderShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.StartupFolderCommon, list);

            // 4. Scheduled Tasks AtLogon
            ReadScheduledTasksAtLogon(list);

            return list;
        });
    }

    public async Task<(bool Success, string Message)> DisableEntryAsync(StartupEntry entry, string sessionId)
    {
        try
        {
            switch (entry.Source)
            {
                case StartupSource.RegistryRunUser:
                case StartupSource.RegistryRunMachine:
                    return await DisableRegistryEntryAsync(entry, sessionId);

                case StartupSource.StartupFolderUser:
                case StartupSource.StartupFolderCommon:
                    return await DisableShortcutEntryAsync(entry, sessionId);

                case StartupSource.ScheduledTask:
                    return await DisableScheduledTaskEntryAsync(entry, sessionId);

                default:
                    return (false, "Unsupported startup source.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to disable startup item '{entry.Name}': {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> EnableEntryAsync(StartupEntry entry, string sessionId)
    {
        try
        {
            switch (entry.Source)
            {
                case StartupSource.RegistryRunUser:
                case StartupSource.RegistryRunMachine:
                    return await EnableRegistryEntryAsync(entry, sessionId);

                case StartupSource.StartupFolderUser:
                case StartupSource.StartupFolderCommon:
                    return await EnableShortcutEntryAsync(entry, sessionId);

                case StartupSource.ScheduledTask:
                    return await EnableScheduledTaskEntryAsync(entry, sessionId);

                default:
                    return (false, "Unsupported startup source.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to enable startup item '{entry.Name}': {ex.Message}");
        }
    }

    private void ReadRegistryRunKey(RegistryKey rootKey, string subKeyPath, StartupSource source, List<StartupEntry> list)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var val = key.GetValue(name)?.ToString() ?? string.Empty;
                    list.Add(new StartupEntry
                    {
                        Id = $"{source}_{name}",
                        Name = name,
                        PublisherOrPath = val,
                        Source = source,
                        Risk = EvaluateRisk(name, val),
                        IsEnabled = true,
                        OriginalLocation = subKeyPath
                    });
                }
            }

            // Also check disabled backup key
            using var disabledKey = Registry.CurrentUser.OpenSubKey(@"Software\Lumen\DisabledStartup", false);
            if (disabledKey != null)
            {
                foreach (var name in disabledKey.GetValueNames())
                {
                    var val = disabledKey.GetValue(name)?.ToString() ?? string.Empty;
                    if (!list.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new StartupEntry
                        {
                            Id = $"{source}_{name}",
                            Name = name,
                            PublisherOrPath = val,
                            Source = source,
                            Risk = EvaluateRisk(name, val),
                            IsEnabled = false,
                            OriginalLocation = subKeyPath
                        });
                    }
                }
            }
        }
        catch { }
    }

    private void ReadStartupFolderShortcuts(string folderPath, StartupSource source, List<StartupEntry> list)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                foreach (var file in Directory.EnumerateFiles(folderPath, "*.lnk"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    list.Add(new StartupEntry
                    {
                        Id = $"{source}_{name}",
                        Name = name,
                        PublisherOrPath = file,
                        Source = source,
                        Risk = EvaluateRisk(name, file),
                        IsEnabled = true,
                        OriginalLocation = file
                    });
                }

                foreach (var file in Directory.EnumerateFiles(folderPath, "*.lumendisabled"))
                {
                    var name = Path.GetFileNameWithoutExtension(file).Replace(".lnk", "", StringComparison.OrdinalIgnoreCase);
                    list.Add(new StartupEntry
                    {
                        Id = $"{source}_{name}",
                        Name = name,
                        PublisherOrPath = file,
                        Source = source,
                        Risk = EvaluateRisk(name, file),
                        IsEnabled = false,
                        OriginalLocation = file
                    });
                }
            }
        }
        catch { }
    }

    private void ReadScheduledTasksAtLogon(List<StartupEntry> list)
    {
        try
        {
            using var ts = new TaskService();
            foreach (var task in ts.AllTasks)
            {
                try
                {
                    if (task.Definition.Triggers.Any(t => t.TriggerType == TaskTriggerType.Logon))
                    {
                        list.Add(new StartupEntry
                        {
                            Id = $"ScheduledTask_{task.Name}",
                            Name = task.Name,
                            PublisherOrPath = task.Path,
                            Source = StartupSource.ScheduledTask,
                            Risk = EvaluateRisk(task.Name, task.Path),
                            IsEnabled = task.Enabled,
                            OriginalLocation = task.Path
                        });
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private RiskLevel EvaluateRisk(string name, string path)
    {
        foreach (var kvp in CuratedRiskDictionary)
        {
            if (name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) || path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return RiskLevel.Unknown;
    }

    private async Task<(bool Success, string Message)> DisableRegistryEntryAsync(StartupEntry entry, string sessionId)
    {
        // 1. Reversible Backup Key move
        using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true) ?? Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
        {
            if (runKey != null)
            {
                var val = runKey.GetValue(entry.Name)?.ToString();
                if (val != null)
                {
                    using var disabledKey = Registry.CurrentUser.CreateSubKey(@"Software\Lumen\DisabledStartup", true);
                    disabledKey.SetValue(entry.Name, val);
                    runKey.DeleteValue(entry.Name, false);
                }
            }
        }

        entry.IsEnabled = false;

        // Log action
        await _sessionLogService.LogActionAsync(new ActionRecord
        {
            SessionId = sessionId,
            Module = "Startup",
            ActionType = "DisableRegistryRun",
            TargetName = entry.Name,
            Details = entry.PublisherOrPath,
            BeforeStateJson = JsonSerializer.Serialize(entry.PublisherOrPath),
            IsReversible = true,
            IsUndone = false
        });

        return (true, $"Disabled registry startup entry '{entry.Name}'.");
    }

    private async Task<(bool Success, string Message)> EnableRegistryEntryAsync(StartupEntry entry, string sessionId)
    {
        using (var disabledKey = Registry.CurrentUser.OpenSubKey(@"Software\Lumen\DisabledStartup", true))
        {
            if (disabledKey != null)
            {
                var val = disabledKey.GetValue(entry.Name)?.ToString();
                if (val != null)
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    if (runKey != null)
                    {
                        runKey.SetValue(entry.Name, val);
                        disabledKey.DeleteValue(entry.Name, false);
                    }
                }
            }
        }

        entry.IsEnabled = true;

        await _sessionLogService.LogActionAsync(new ActionRecord
        {
            SessionId = sessionId,
            Module = "Startup",
            ActionType = "EnableRegistryRun",
            TargetName = entry.Name,
            Details = entry.PublisherOrPath,
            BeforeStateJson = string.Empty,
            IsReversible = true,
            IsUndone = false
        });

        return (true, $"Enabled registry startup entry '{entry.Name}'.");
    }

    private async Task<(bool Success, string Message)> DisableShortcutEntryAsync(StartupEntry entry, string sessionId)
    {
        if (File.Exists(entry.OriginalLocation) && entry.OriginalLocation.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var disabledPath = entry.OriginalLocation.Replace(".lnk", ".lumendisabled", StringComparison.OrdinalIgnoreCase);
            File.Move(entry.OriginalLocation, disabledPath, true);
            entry.OriginalLocation = disabledPath;
        }

        entry.IsEnabled = false;

        await _sessionLogService.LogActionAsync(new ActionRecord
        {
            SessionId = sessionId,
            Module = "Startup",
            ActionType = "DisableShortcut",
            TargetName = entry.Name,
            Details = entry.OriginalLocation,
            BeforeStateJson = JsonSerializer.Serialize(entry.OriginalLocation),
            IsReversible = true,
            IsUndone = false
        });

        return (true, $"Disabled startup shortcut '{entry.Name}'.");
    }

    private async Task<(bool Success, string Message)> EnableShortcutEntryAsync(StartupEntry entry, string sessionId)
    {
        if (File.Exists(entry.OriginalLocation) && entry.OriginalLocation.EndsWith(".lumendisabled", StringComparison.OrdinalIgnoreCase))
        {
            var enabledPath = entry.OriginalLocation.Replace(".lumendisabled", ".lnk", StringComparison.OrdinalIgnoreCase);
            File.Move(entry.OriginalLocation, enabledPath, true);
            entry.OriginalLocation = enabledPath;
        }

        entry.IsEnabled = true;

        await _sessionLogService.LogActionAsync(new ActionRecord
        {
            SessionId = sessionId,
            Module = "Startup",
            ActionType = "EnableShortcut",
            TargetName = entry.Name,
            Details = entry.OriginalLocation,
            BeforeStateJson = string.Empty,
            IsReversible = true,
            IsUndone = false
        });

        return (true, $"Enabled startup shortcut '{entry.Name}'.");
    }

    private async Task<(bool Success, string Message)> DisableScheduledTaskEntryAsync(StartupEntry entry, string sessionId)
    {
        using var ts = new TaskService();
        var task = ts.GetTask(entry.Name);
        if (task != null)
        {
            task.Enabled = false;
            task.RegisterChanges();
        }

        entry.IsEnabled = false;

        await _sessionLogService.LogActionAsync(new ActionRecord
        {
            SessionId = sessionId,
            Module = "Startup",
            ActionType = "DisableScheduledTask",
            TargetName = entry.Name,
            Details = entry.PublisherOrPath,
            BeforeStateJson = "true",
            IsReversible = true,
            IsUndone = false
        });

        return (true, $"Disabled scheduled task '{entry.Name}'.");
    }

    private async Task<(bool Success, string Message)> EnableScheduledTaskEntryAsync(StartupEntry entry, string sessionId)
    {
        using var ts = new TaskService();
        var task = ts.GetTask(entry.Name);
        if (task != null)
        {
            task.Enabled = true;
            task.RegisterChanges();
        }

        entry.IsEnabled = true;

        await _sessionLogService.LogActionAsync(new ActionRecord
        {
            SessionId = sessionId,
            Module = "Startup",
            ActionType = "EnableScheduledTask",
            TargetName = entry.Name,
            Details = entry.PublisherOrPath,
            BeforeStateJson = "false",
            IsReversible = true,
            IsUndone = false
        });

        return (true, $"Enabled scheduled task '{entry.Name}'.");
    }
}
