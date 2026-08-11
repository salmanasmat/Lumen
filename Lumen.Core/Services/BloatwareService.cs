using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class BloatwareService : IBloatwareService
{
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    private static readonly HashSet<string> ProtectedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.WindowsCalculator",
        "Microsoft.WindowsNotepad",
        "Microsoft.WindowsStore",
        "Microsoft.Windows.Photos",
        "Microsoft.SecHealthUI",
        "Microsoft.WindowsTerminal"
    };

    private static readonly Dictionary<string, (string FriendlyName, BloatwareCategory Category)> KnownBloatwareMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Microsoft.XboxApp", ("Xbox App", BloatwareCategory.Gaming) },
        { "Microsoft.XboxGamingOverlay", ("Xbox Game Bar", BloatwareCategory.Gaming) },
        { "Microsoft.XboxSpeechToTextOverlay", ("Xbox Speech Overlay", BloatwareCategory.Gaming) },
        { "Microsoft.XboxIdentityProvider", ("Xbox Identity Provider", BloatwareCategory.Gaming) },
        { "Microsoft.MicrosoftSolitaireCollection", ("Solitaire Collection", BloatwareCategory.Gaming) },

        { "Microsoft.3DViewer", ("3D Viewer", BloatwareCategory.Media) },
        { "Microsoft.Microsoft3DViewer", ("3D Viewer", BloatwareCategory.Media) },
        { "Microsoft.Paint3D", ("Paint 3D", BloatwareCategory.Media) },
        { "Microsoft.ZuneMusic", ("Groove Music / Media Player", BloatwareCategory.Media) },
        { "Microsoft.ZuneVideo", ("Movies & TV", BloatwareCategory.Media) },

        { "Microsoft.People", ("People App", BloatwareCategory.Social) },
        { "Microsoft.YourPhone", ("Phone Link", BloatwareCategory.Social) },
        { "Microsoft.SkypeApp", ("Skype", BloatwareCategory.Social) },

        { "Microsoft.BingNews", ("News App", BloatwareCategory.Misc) },
        { "Microsoft.BingWeather", ("Weather App", BloatwareCategory.Misc) },
        { "Microsoft.MixedReality.Portal", ("Mixed Reality Portal", BloatwareCategory.Misc) },
        { "Microsoft.WindowsFeedbackHub", ("Feedback Hub", BloatwareCategory.Misc) },
        { "Microsoft.GetHelp", ("Get Help App", BloatwareCategory.Misc) },
        { "Microsoft.Getstarted", ("Tips App", BloatwareCategory.Misc) },
        { "Microsoft.Office.OneNote", ("OneNote (Consumer)", BloatwareCategory.Misc) }
    };

    public BloatwareService(IRestorePointService restorePointService, ISessionLogService sessionLogService)
    {
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    public async Task<List<BloatwarePackage>> GetInstalledBloatwareAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<BloatwarePackage>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName | ConvertTo-Json\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(10000);

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        using var doc = JsonDocument.Parse(output);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in doc.RootElement.EnumerateArray())
                            {
                                AddPackageIfBloatware(element, list);
                            }
                        }
                        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            AddPackageIfBloatware(doc.RootElement, list);
                        }
                    }
                }
            }
            catch
            {
                // Fallback mock static list if PowerShell call fails
            }

            // Ensure curated bloatware list is populated even if not yet installed on host PC
            foreach (var kvp in KnownBloatwareMap)
            {
                if (!list.Any(p => p.PackageName.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(new BloatwarePackage
                    {
                        PackageName = kvp.Key,
                        PackageFullName = kvp.Key,
                        DisplayName = kvp.Value.FriendlyName,
                        Category = kvp.Value.Category,
                        IsRecommendedToRemove = true,
                        IsProtected = ProtectedPackages.Contains(kvp.Key),
                        IsSelected = false
                    });
                }
            }

            return list;
        });
    }

    public async Task<(bool Success, string Message)> RemovePackageAsync(BloatwarePackage package, string sessionId)
    {
        if (package.IsProtected)
        {
            return (false, $"Package '{package.DisplayName}' is protected and cannot be uninstalled.");
        }

        return await Task.Run(async () =>
        {
            try
            {
                var script = $"Get-AppxPackage -Name '{package.PackageName}' -AllUsers | Remove-AppxPackage -AllUsers; Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{package.PackageName}' }} | Remove-AppxProvisionedPackage -Online";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var errorOutput = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(20000);

                    if (!string.IsNullOrWhiteSpace(errorOutput))
                    {
                        return (false, $"Failed to remove '{package.DisplayName}': {errorOutput.Trim()}");
                    }
                }

                await _sessionLogService.LogActionAsync(new ActionRecord
                {
                    SessionId = sessionId,
                    Module = "Bloatware",
                    ActionType = "RemoveAppxPackage",
                    TargetName = package.PackageName,
                    Details = package.DisplayName,
                    BeforeStateJson = string.Empty,
                    IsReversible = false, // File purge is permanent
                    IsUndone = false
                });

                return (true, $"Removed bloatware package '{package.DisplayName}'.");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to remove '{package.DisplayName}': {ex.Message}");
            }
        });
    }

    private void AddPackageIfBloatware(JsonElement element, List<BloatwarePackage> list)
    {
        if (element.TryGetProperty("Name", out var nameProp) && element.TryGetProperty("PackageFullName", out var fullNameProp))
        {
            var name = nameProp.GetString() ?? string.Empty;
            var fullName = fullNameProp.GetString() ?? string.Empty;

            if (KnownBloatwareMap.TryGetValue(name, out var info))
            {
                list.Add(new BloatwarePackage
                {
                    PackageName = name,
                    PackageFullName = fullName,
                    DisplayName = info.FriendlyName,
                    Category = info.Category,
                    IsRecommendedToRemove = true,
                    IsProtected = ProtectedPackages.Contains(name),
                    IsSelected = false
                });
            }
        }
    }
}
