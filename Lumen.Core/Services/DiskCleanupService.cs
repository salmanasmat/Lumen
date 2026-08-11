using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class DiskCleanupService : IDiskCleanupService
{
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public DiskCleanupService(IRestorePointService restorePointService, ISessionLogService sessionLogService)
    {
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    public async Task<List<CleanupCategoryItem>> CalculateReclaimableSizesAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<CleanupCategoryItem>();

            // 1. User Temp
            var userTemp = Path.GetTempPath();
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.UserTemp,
                Name = "User Temporary Files (%TEMP%)",
                Description = "Application temporary files and caches in user profile.",
                SizeMb = Math.Round(GetDirectorySizeMb(userTemp), 1),
                IsSafePreset = true,
                IsHighRisk = false,
                IsSelected = true
            });

            // 2. System Temp
            var sysTemp = @"C:\Windows\Temp";
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.SystemTemp,
                Name = "System Temporary Files (C:\\Windows\\Temp)",
                Description = "Windows system temporary files and update logs.",
                SizeMb = Math.Round(GetDirectorySizeMb(sysTemp), 1),
                IsSafePreset = true,
                IsHighRisk = false,
                IsSelected = true
            });

            // 3. Chrome Cache
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var chromeCache = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache");
            var chromeCodeCache = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache");
            double chromeSize = GetDirectorySizeMb(chromeCache) + GetDirectorySizeMb(chromeCodeCache);
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.ChromeCache,
                Name = "Google Chrome Web Cache",
                Description = "Cached images, assets, and code from Chrome web browsing.",
                SizeMb = Math.Round(chromeSize, 1),
                IsSafePreset = true,
                IsHighRisk = false,
                IsSelected = true
            });

            // 4. Recycle Bin
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.RecycleBin,
                Name = "Recycle Bin",
                Description = "Deleted files stored in Recycle Bin.",
                SizeMb = 0, // P/Invoke clean handles total empty
                IsSafePreset = true,
                IsHighRisk = false,
                IsSelected = true
            });

            // 5. Windows Error Reporting
            var werPath = @"C:\ProgramData\Microsoft\Windows\WER";
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.WerReports,
                Name = "Windows Error Reporting Logs (WER)",
                Description = "System crash dumps and error report queues.",
                SizeMb = Math.Round(GetDirectorySizeMb(werPath), 1),
                IsSafePreset = true,
                IsHighRisk = false,
                IsSelected = true
            });

            // 6. DISM Component Cleanup
            var winSxS = @"C:\Windows\WinSxS";
            double winSxSSizeMb = GetDirectorySizeMb(winSxS);
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.DismComponentCleanup,
                Name = "Windows Component Store (DISM ResetBase)",
                Description = "Superseded Windows update components. Note: Prevents uninstalling past Windows Updates.",
                SizeMb = Math.Round(winSxSSizeMb * 0.2, 1), // Estimate 20% reclaimable
                IsSafePreset = false,
                IsHighRisk = true,
                IsSelected = false
            });

            // 7. Windows.old
            var winOld = @"C:\Windows.old";
            bool hasWinOld = Directory.Exists(winOld);
            list.Add(new CleanupCategoryItem
            {
                Type = CleanupType.WindowsOld,
                Name = "Previous Windows Installation (C:\\Windows.old)",
                Description = "Backup folder from previous OS upgrades. Note: Prevents OS rollback.",
                SizeMb = hasWinOld ? Math.Round(GetDirectorySizeMb(winOld), 1) : 0,
                IsSafePreset = false,
                IsHighRisk = true,
                IsSelected = false
            });

            return list;
        });
    }

    public async Task<(bool Success, string Message)> ExecuteCleanupAsync(List<CleanupCategoryItem> selectedCategories, string sessionId)
    {
        double totalFreedMb = 0;
        int cleanedCount = 0;

        foreach (var category in selectedCategories)
        {
            try
            {
                switch (category.Type)
                {
                    case CleanupType.UserTemp:
                        totalFreedMb += await Task.Run(() => DeleteDirectoryContents(Path.GetTempPath()));
                        cleanedCount++;
                        break;

                    case CleanupType.SystemTemp:
                        totalFreedMb += await Task.Run(() => DeleteDirectoryContents(@"C:\Windows\Temp"));
                        cleanedCount++;
                        break;

                    case CleanupType.ChromeCache:
                        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        totalFreedMb += await Task.Run(() => DeleteDirectoryContents(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache")));
                        totalFreedMb += await Task.Run(() => DeleteDirectoryContents(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache")));
                        cleanedCount++;
                        break;

                    case CleanupType.RecycleBin:
                        await Task.Run(() => EmptyRecycleBin());
                        cleanedCount++;
                        break;

                    case CleanupType.WerReports:
                        totalFreedMb += await Task.Run(() => DeleteDirectoryContents(@"C:\ProgramData\Microsoft\Windows\WER"));
                        cleanedCount++;
                        break;

                    case CleanupType.DismComponentCleanup:
                        await Task.Run(() => RunDismCleanup());
                        cleanedCount++;
                        break;

                    case CleanupType.WindowsOld:
                        totalFreedMb += await Task.Run(() => DeleteWindowsOldFolder());
                        cleanedCount++;
                        break;
                }

                await _sessionLogService.LogActionAsync(new ActionRecord
                {
                    SessionId = sessionId,
                    Module = "DiskCleanup",
                    ActionType = "CleanCategory",
                    TargetName = category.Name,
                    Details = $"Freed ~{category.SizeMb} MB",
                    BeforeStateJson = string.Empty,
                    IsReversible = false,
                    IsUndone = false
                });
            }
            catch { }
        }

        return (true, $"Disk cleanup completed: cleaned {cleanedCount} category/categories (~{Math.Round(totalFreedMb / 1024, 2)} GB reclaimed).");
    }

    private double GetDirectorySizeMb(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            var dir = new DirectoryInfo(path);
            long bytes = GetDirLength(dir);
            return bytes / (1024.0 * 1024.0);
        }
        catch
        {
            return 0;
        }
    }

    private long GetDirLength(DirectoryInfo dir)
    {
        long len = 0;
        try
        {
            foreach (var f in dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                try { len += f.Length; } catch { }
            }
            foreach (var d in dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                try { len += GetDirLength(d); } catch { }
            }
        }
        catch { }
        return len;
    }

    private double DeleteDirectoryContents(string path)
    {
        if (!Directory.Exists(path)) return 0;
        double freedBytes = 0;
        var dir = new DirectoryInfo(path);

        foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            try
            {
                long len = file.Length;
                file.Delete();
                freedBytes += len;
            }
            catch
            {
                // Silently skip locked/in-use files
            }
        }

        return freedBytes / (1024.0 * 1024.0);
    }

    private void EmptyRecycleBin()
    {
        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        }
        catch { }
    }

    private void RunDismCleanup()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = "/Online /Cleanup-Image /StartComponentCleanup /ResetBase",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(60000);
        }
        catch { }
    }

    private double DeleteWindowsOldFolder()
    {
        var path = @"C:\Windows.old";
        if (!Directory.Exists(path)) return 0;

        double sizeMb = GetDirectorySizeMb(path);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c takeown /F \"{path}\" /A /R /D Y & icacls \"{path}\" /grant Administrators:F /T & rmdir /S /Q \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        catch { }

        return sizeMb;
    }
}
