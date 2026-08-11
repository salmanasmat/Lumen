using System.Collections.Generic;
using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IBloatwareService
{
    Task<List<BloatwarePackage>> GetInstalledBloatwareAsync();
    Task<(bool Success, string Message)> RemovePackageAsync(BloatwarePackage package, string sessionId);
}
