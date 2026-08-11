using System.Collections.Generic;
using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface IStartupService
{
    Task<List<StartupEntry>> GetStartupEntriesAsync();
    Task<(bool Success, string Message)> DisableEntryAsync(StartupEntry entry, string sessionId);
    Task<(bool Success, string Message)> EnableEntryAsync(StartupEntry entry, string sessionId);
}
