using System.Collections.Generic;
using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IServicesService
{
    Task<List<ServiceItem>> GetServicesAsync();
    Task<(bool Success, string Message)> ChangeServiceStartTypeAsync(ServiceItem service, ServiceStartType newStartType, string sessionId);
    Task<(bool Success, string Message)> ApplySafePresetAsync(string sessionId);
}
