# Lumen — PC Health & Debloat Toolkit

**Lumen** is a native Windows (.NET 8 WPF) optimization and debloating utility tailored for office workstations running Google Chrome, Microsoft Office, and SmarTerm terminal emulator.

---

## Key Features

- **Diagnostics Scorecard**: Read-only evaluation of boot performance (Event ID 100), storage media type (SSD vs HDD), RAM/CPU stats, drive free space, WinSxS size, antivirus status, and pending reboots.
- **Safety-First Architecture**: Every mutating operation automatically checks/creates a **System Restore Point** via PowerShell (`Checkpoint-Computer`) and logs before-states to SQLite (`lumen.db`).
- **Reversible Startup Manager**: Disables registry run entries into `HKCU\Software\Lumen\DisabledStartup` backups, renames `.lnk` shortcuts to `.lumendisabled`, and toggles Task Scheduler logon triggers without destructive deletion.
- **Office Bloatware Removal**: Office-safe uninstall checklist targeting ~25 pre-installed AppX packages (Xbox, 3D Viewer, Solitaire, News, Weather) with strict protection for core apps (Calculator, Notepad, Store, Photos, Security).
- **Disk Cleanup**: Size pre-calculations and cleanups for `%TEMP%`, `C:\Windows\Temp`, Chrome cache, Recycle Bin, WER queues, DISM component cleanup (`/ResetBase`), and `Windows.old`.
- **Services Tuner**: Office-only safe preset (`Fax`, `RemoteRegistry`, `DiagTrack`, `XblAuthManager`) alongside an immutable **`NEVER_TOUCH`** list protecting network stack (`Dhcp`, `Dnscache`, `NlaSvc`), RDP/SmarTerm dependencies (`TermService`), and security (`WinDefend`, `wuauserv`).
- **Network & Logon Diagnostics**: Measures DNS resolution time, server ICMP ping latency, and inspects mapped network drives (specifically monitoring `Z:\`) for reachability and reconnect-at-logon delays.
- **Optimization Profiles**: Includes built-in `"Office Terminal Workstation"` profile and JSON import/export for batch one-click fleet execution.
- **Fleet Rollout & CLI Mode**: Run interactively or silently in headless mode:
  ```powershell
  .\Lumen.exe --profile "Office Terminal Workstation" --silent
  ```

---

## Prerequisites & Requirements

- **Operating System**: Windows 10 (1809+) or Windows 11 (x64)
- **Privileges**: Administrator Rights (`requireAdministrator` app manifest elevation required)
- **Framework**: .NET 8 Runtime (or publish as self-contained single-file executable)

---

## Build & Test Instructions

### Build Solution
```powershell
dotnet build Lumen.sln
```

### Run Unit Tests
```powershell
dotnet test Lumen.Tests/Lumen.Tests.csproj
```

### Publish Single-File Executable
```powershell
dotnet publish Lumen.App/Lumen.App.csproj -c Release -r win-x64 --self-contained
```

---

## Session Audit Logs & Safety Locations

- **Database**: `%LocalAppData%\Lumen\lumen.db`
- **Crash Log**: `%LocalAppData%\Lumen\crash.log`
- **Silent Run Logs**: `%LocalAppData%\Lumen\Logs\<timestamp>-silent-run.log`
- **Settings**: `%LocalAppData%\Lumen\settings.json`
