using System.Collections.Generic;
using System.Threading.Tasks;
using Lumen.Core.Models;

namespace Lumen.Core.Interfaces;

public interface ISessionLogService
{
    Task<SessionRecord> StartSessionAsync(string description, bool restorePointCreated);
    Task LogActionAsync(ActionRecord action);
    Task<List<SessionRecord>> GetAllSessionsAsync();
    Task<SessionRecord?> GetSessionByIdAsync(string sessionId);
}
