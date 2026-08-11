using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Models;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class SessionLogAndUndoServiceTests
{
    [Fact]
    public async Task StartSessionAndLogAction_PersistsAndRetrievesCorrectly()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_session_{Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var sessionService = new SessionLogService(dataStore);

            // Act
            var session = await sessionService.StartSessionAsync("Unit Test Optimization Session", true);
            var action = new ActionRecord
            {
                SessionId = session.Id,
                Module = "Startup",
                ActionType = "DisableRegistryRun",
                TargetName = "TestApp",
                Details = "C:\\TestApp\\test.exe",
                BeforeStateJson = "\"C:\\TestApp\\test.exe\"",
                IsReversible = true,
                IsUndone = false
            };
            await sessionService.LogActionAsync(action);

            // Assert
            var sessions = await sessionService.GetAllSessionsAsync();
            Assert.Single(sessions);
            Assert.Equal("Unit Test Optimization Session", sessions[0].Description);
            Assert.True(sessions[0].SystemRestorePointCreated);
            Assert.Single(sessions[0].Actions);
            Assert.Equal("TestApp", sessions[0].Actions[0].TargetName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
