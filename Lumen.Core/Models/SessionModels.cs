using System;
using System.Collections.Generic;

namespace Lumen.Core.Models;

public class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;
    public bool SystemRestorePointCreated { get; set; }
    public List<ActionRecord> Actions { get; set; } = new();
}

public class ActionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Module { get; set; } = string.Empty; // Startup, Bloatware, DiskCleanup, Services, Network
    public string ActionType { get; set; } = string.Empty; // Disable, Remove, Delete, ChangeStartType
    public string TargetName { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string BeforeStateJson { get; set; } = string.Empty;
    public bool IsReversible { get; set; }
    public bool IsUndone { get; set; }
}
