using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class ProfileService : IProfileService
{
    private readonly IStartupService _startupService;
    private readonly IBloatwareService _bloatwareService;
    private readonly IDiskCleanupService _diskCleanupService;
    private readonly IServicesService _servicesService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    public ProfileService(
        IStartupService startupService,
        IBloatwareService bloatwareService,
        IDiskCleanupService diskCleanupService,
        IServicesService servicesService,
        IRestorePointService restorePointService,
        ISessionLogService sessionLogService)
    {
        _startupService = startupService;
        _bloatwareService = bloatwareService;
        _diskCleanupService = diskCleanupService;
        _servicesService = servicesService;
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    public async Task<LumenProfile> GetDefaultProfileAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Lumen.Core.Resources.office_terminal_workstation.json";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var profile = JsonSerializer.Deserialize<LumenProfile>(json);
                    if (profile != null) return profile;
                }
            }
            catch { }

            // Fallback hardcoded default
            return new LumenProfile
            {
                Name = "Office Terminal Workstation",
                Description = "Built-in workstation profile for office PCs.",
                IsBuiltIn = true,
                DisabledServices = ServicesService.SafeToDisablePreset.ToList(),
                DisabledStartupIdentifiers = new List<string> { "OneDrive", "Adobe", "Spotify" },
                RemovedBloatwarePackages = new List<string> { "Microsoft.XboxApp", "Microsoft.3DViewer", "Microsoft.BingNews" }
            };
        });
    }

    public async Task<List<LumenProfile>> GetCustomProfilesAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<LumenProfile>();
            try
            {
                var profilesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "Profiles");
                if (Directory.Exists(profilesDir))
                {
                    foreach (var file in Directory.EnumerateFiles(profilesDir, "*.json"))
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var p = JsonSerializer.Deserialize<LumenProfile>(json);
                            if (p != null) list.Add(p);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return list;
        });
    }

    public async Task SaveProfileAsync(LumenProfile profile, string filePath)
    {
        await Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        });
    }

    public async Task<LumenProfile> LoadProfileAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<LumenProfile>(json);
            return profile ?? throw new InvalidDataException("Invalid profile JSON.");
        });
    }

    public async Task<(bool Success, string Message)> ApplyProfileAsync(LumenProfile profile, IProgress<string>? progress = null)
    {
        progress?.Report("Creating System Restore Point up-front...");
        await _restorePointService.CreateRestorePointAsync($"Apply Profile: {profile.Name}");

        var session = await _sessionLogService.StartSessionAsync($"Apply Profile '{profile.Name}'", true);

        // 1. Startup Entries
        progress?.Report("Applying startup optimizations...");
        try
        {
            var startupItems = await _startupService.GetStartupEntriesAsync();
            foreach (var item in startupItems)
            {
                if (profile.DisabledStartupIdentifiers.Any(id => item.Name.Contains(id, StringComparison.OrdinalIgnoreCase)))
                {
                    await _startupService.DisableEntryAsync(item, session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Startup step error: {ex.Message}");
        }

        // 2. Services
        progress?.Report("Applying services tuner optimizations...");
        try
        {
            var services = await _servicesService.GetServicesAsync();
            foreach (var svc in services)
            {
                if (profile.DisabledServices.Contains(svc.ServiceName) && !svc.IsNeverTouch)
                {
                    await _servicesService.ChangeServiceStartTypeAsync(svc, ServiceStartType.Disabled, session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Services step error: {ex.Message}");
        }

        // 3. Bloatware Removal
        progress?.Report("Purging bloatware packages...");
        try
        {
            var packages = await _bloatwareService.GetInstalledBloatwareAsync();
            foreach (var pkg in packages)
            {
                if (profile.RemovedBloatwarePackages.Any(p => pkg.PackageName.Equals(p, StringComparison.OrdinalIgnoreCase)))
                {
                    await _bloatwareService.RemovePackageAsync(pkg, session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Bloatware step error: {ex.Message}");
        }

        // 4. Disk Cleanup
        progress?.Report("Executing disk cleanup...");
        try
        {
            var categories = await _diskCleanupService.CalculateReclaimableSizesAsync();
            var selected = categories.Where(c => profile.SelectedCleanupCategories.Contains(c.Type)).ToList();
            if (selected.Any())
            {
                await _diskCleanupService.ExecuteCleanupAsync(selected, session.Id);
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Disk cleanup step error: {ex.Message}");
        }

        progress?.Report($"Profile '{profile.Name}' applied successfully.");
        return (true, $"Optimization profile '{profile.Name}' applied successfully.");
    }
}
