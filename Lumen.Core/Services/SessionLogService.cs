using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumen.Core.Data;
using Lumen.Core.Interfaces;
using Lumen.Core.Models;

namespace Lumen.Core.Services;

public class SessionLogService : ISessionLogService
{
    private readonly SqliteDataStore _dataStore;

    public SessionLogService(SqliteDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task<SessionRecord> StartSessionAsync(string description, bool restorePointCreated)
    {
        var session = new SessionRecord
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.Now,
            Description = description,
            SystemRestorePointCreated = restorePointCreated
        };

        await _dataStore.SaveSessionAsync(session);
        return session;
    }

    public async Task LogActionAsync(ActionRecord action)
    {
        if (string.IsNullOrWhiteSpace(action.Id))
        {
            action.Id = Guid.NewGuid().ToString();
        }
        if (action.Timestamp == default)
        {
            action.Timestamp = DateTime.Now;
        }

        await _dataStore.SaveActionAsync(action);
    }

    public async Task<List<SessionRecord>> GetAllSessionsAsync()
    {
        return await _dataStore.GetAllSessionsAsync();
    }

    public async Task<SessionRecord?> GetSessionByIdAsync(string sessionId)
    {
        var sessions = await _dataStore.GetAllSessionsAsync();
        return sessions.FirstOrDefault(s => s.Id == sessionId);
    }
}
