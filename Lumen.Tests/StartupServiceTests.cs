using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Models;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class StartupServiceTests
{
    [Fact]
    public async Task GetStartupEntriesAsync_ReturnsEntriesList()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_startup_{System.Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var restoreService = new RestorePointService();
            var logService = new SessionLogService(dataStore);
            var startupService = new StartupService(restoreService, logService);

            // Act
            var entries = await startupService.GetStartupEntriesAsync();

            // Assert
            Assert.NotNull(entries);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
