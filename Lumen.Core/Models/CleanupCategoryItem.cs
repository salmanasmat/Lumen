namespace Lumen.Core.Models;

public enum CleanupType
{
    UserTemp,
    SystemTemp,
    ChromeCache,
    DismComponentCleanup,
    WindowsOld,
    RecycleBin,
    WerReports
}

public class CleanupCategoryItem
{
    public CleanupType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double SizeMb { get; set; }
    public bool IsSafePreset { get; set; }
    public bool IsHighRisk { get; set; } // DISM & Windows.old
    public bool IsSelected { get; set; }
}
