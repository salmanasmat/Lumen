using System.Collections.Generic;

namespace Lumen.Core.Models;

public class LumenProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    public List<string> DisabledStartupIdentifiers { get; set; } = new();
    public List<string> RemovedBloatwarePackages { get; set; } = new();
    public List<CleanupType> SelectedCleanupCategories { get; set; } = new();
    public List<string> DisabledServices { get; set; } = new();
}
