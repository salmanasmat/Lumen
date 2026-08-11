namespace Lumen.Core.Models;

public enum BloatwareCategory
{
    Gaming,
    Media,
    Social,
    Misc
}

public class BloatwarePackage
{
    public string PackageFullName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public BloatwareCategory Category { get; set; } = BloatwareCategory.Misc;
    public bool IsRecommendedToRemove { get; set; }
    public bool IsProtected { get; set; }
    public bool IsSelected { get; set; }
}
