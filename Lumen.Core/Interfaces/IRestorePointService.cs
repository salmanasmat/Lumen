using System.Threading.Tasks;

namespace Lumen.Core.Interfaces;

public interface IRestorePointService
{
    Task<bool> IsSystemRestoreEnabledAsync();
    Task<(bool Success, string Message)> CreateRestorePointAsync(string description);
}
