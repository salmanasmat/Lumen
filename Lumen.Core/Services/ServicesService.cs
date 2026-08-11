using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class ServicesService : IServicesService
{
    private readonly IRestorePointService _restorePointService;
    private readonly ISessionLogService _sessionLogService;

    public static readonly HashSet<string> SafeToDisablePreset = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fax",
        "RemoteRegistry",
        "MapsBroker",
        "XblAuthManager",
        "XblGameSave",
        "XboxNetApiSvc",
        "WerSvc",
        "DiagTrack",
        "RetailDemo",
        "PhoneSvc"
    };

    public static readonly HashSet<string> NeverTouchList = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dhcp",
        "Dnscache",
        "NlaSvc",
        "TermService",
        "UmRdpService",
        "RpcSs",
        "RpcEptMapper",
        "wuauserv",
        "WinDefend",
        "MpsSvc",
        "EventLog",
        "PlugPlay",
        "Power"
    };

    public ServicesService(IRestorePointService restorePointService, ISessionLogService sessionLogService)
    {
        _restorePointService = restorePointService;
        _sessionLogService = sessionLogService;
    }

    public async Task<List<ServiceItem>> GetServicesAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<ServiceItem>();

            try
            {
                var controllers = ServiceController.GetServices();
                foreach (var sc in controllers)
                {
                    try
                    {
                        var name = sc.ServiceName;
                        var disp = sc.DisplayName;
                        var status = sc.Status.ToString();

                        var startType = ReadStartTypeFromRegistry(name);

                        var isNeverTouch = NeverTouchList.Contains(name);
                        var isSafe = SafeToDisablePreset.Contains(name);

                        list.Add(new ServiceItem
                        {
                            ServiceName = name,
                            DisplayName = disp,
                            Status = status,
                            CurrentStartType = startType,
                            OriginalStartType = startType,
                            IsSafeToDisable = isSafe,
                            IsNeverTouch = isNeverTouch,
                            IsSelected = false
                        });
                    }
                    catch { }
                }
            }
            catch { }

            return list.OrderBy(s => s.DisplayName).ToList();
        });
    }

    public async Task<(bool Success, string Message)> ChangeServiceStartTypeAsync(ServiceItem service, ServiceStartType newStartType, string sessionId)
    {
        if (service.IsNeverTouch)
        {
            return (false, $"Service '{service.DisplayName}' is protected on the immutable NEVER_TOUCH list and cannot be modified.");
        }

        return await Task.Run(async () =>
        {
            try
            {
                var originalInt = (int)service.CurrentStartType;
                var newInt = (int)newStartType;

                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service.ServiceName}", true);
                if (key != null)
                {
                    key.SetValue("Start", newInt, RegistryValueKind.DWord);

                    // If setting to Disabled, stop running service
                    if (newStartType == ServiceStartType.Disabled)
                    {
                        try
                        {
                            using var sc = new ServiceController(service.ServiceName);
                            if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.Paused)
                            {
                                sc.Stop();
                            }
                        }
                        catch { }
                    }

                    service.CurrentStartType = newStartType;

                    await _sessionLogService.LogActionAsync(new ActionRecord
                    {
                        SessionId = sessionId,
                        Module = "Services",
                        ActionType = "ChangeStartType",
                        TargetName = service.ServiceName,
                        Details = $"Changed start type from {service.CurrentStartType} to {newStartType}",
                        BeforeStateJson = JsonSerializer.Serialize(originalInt),
                        IsReversible = true,
                        IsUndone = false
                    });

                    return (true, $"Changed '{service.DisplayName}' start type to {newStartType}.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to change start type for '{service.DisplayName}': {ex.Message}");
            }

            return (false, "Could not open service registry key.");
        });
    }

    public async Task<(bool Success, string Message)> ApplySafePresetAsync(string sessionId)
    {
        var services = await GetServicesAsync();
        var safeToDisable = services.Where(s => s.IsSafeToDisable && !s.IsNeverTouch && s.CurrentStartType != ServiceStartType.Disabled).ToList();

        if (!safeToDisable.Any())
        {
            return (true, "All office safe-to-disable services are already disabled.");
        }

        int disabledCount = 0;
        foreach (var svc in safeToDisable)
        {
            var (res, _) = await ChangeServiceStartTypeAsync(svc, ServiceStartType.Disabled, sessionId);
            if (res) disabledCount++;
        }

        return (true, $"Applied Office-Only Preset: disabled {disabledCount} service(s).");
    }

    private ServiceStartType ReadStartTypeFromRegistry(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", false);
            if (key != null)
            {
                var val = Convert.ToInt32(key.GetValue("Start") ?? 0);
                return val switch
                {
                    2 => ServiceStartType.Automatic,
                    3 => ServiceStartType.Manual,
                    4 => ServiceStartType.Disabled,
                    _ => ServiceStartType.Unknown
                };
            }
        }
        catch { }
        return ServiceStartType.Unknown;
    }
}
