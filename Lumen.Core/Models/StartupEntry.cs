namespace Lumen.Core.Models;

public enum StartupSource
{
    RegistryRunUser,
    RegistryRunMachine,
    StartupFolderUser,
    StartupFolderCommon,
    ScheduledTask
}

public enum RiskLevel
{
    Safe,
    Caution,
    Unknown
}

public class StartupEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PublisherOrPath { get; set; } = string.Empty;
    public StartupSource Source { get; set; }
    public RiskLevel Risk { get; set; } = RiskLevel.Unknown;
    public bool IsEnabled { get; set; } = true;
    public string OriginalLocation { get; set; } = string.Empty;
}
