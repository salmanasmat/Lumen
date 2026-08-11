namespace Lumen.Core.Models;

public enum ServiceStartType
{
    Automatic = 2,
    Manual = 3,
    Disabled = 4,
    Unknown = 0
}

public class ServiceItem
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Stopped"; // Running / Stopped
    public ServiceStartType CurrentStartType { get; set; }
    public ServiceStartType OriginalStartType { get; set; }
    public bool IsSafeToDisable { get; set; }
    public bool IsNeverTouch { get; set; } // Immutable protection
    public bool IsSelected { get; set; }
}
