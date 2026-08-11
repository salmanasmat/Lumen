using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lumen.App.ViewModels;

public class AppSettingsModel
{
    public string ServerTarget { get; set; } = string.Empty;
    public string MonitoredDrive { get; set; } = "Z:";
    public bool AutoCheckSystemRestore { get; set; } = true;
}

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Settings & Configuration";

    [ObservableProperty]
    private string _statusText = "Configure fleet and diagnostic parameters.";

    [ObservableProperty]
    private string _serverTarget = string.Empty;

    [ObservableProperty]
    private string _monitoredDrive = "Z:";

    [ObservableProperty]
    private bool _autoCheckSystemRestore = true;

    private readonly string _settingsFilePath;

    public SettingsViewModel()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
        LoadSettings();
    }

    [RelayCommand]
    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettingsModel>(json);
                if (settings != null)
                {
                    ServerTarget = settings.ServerTarget;
                    MonitoredDrive = settings.MonitoredDrive;
                    AutoCheckSystemRestore = settings.AutoCheckSystemRestore;
                    StatusText = "Settings loaded.";
                    return;
                }
            }
        }
        catch { }

        StatusText = "Default settings initialized.";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new AppSettingsModel
            {
                ServerTarget = ServerTarget,
                MonitoredDrive = MonitoredDrive,
                AutoCheckSystemRestore = AutoCheckSystemRestore
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
            StatusText = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to save settings: {ex.Message}";
        }
    }
}
