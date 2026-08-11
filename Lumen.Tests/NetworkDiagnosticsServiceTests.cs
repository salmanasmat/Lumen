using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class NetworkDiagnosticsServiceTests
{
    [Fact]
    public async Task RunNetworkDiagnosticsAsync_ExecutesAndReturnsResult()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_net_{System.Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var logService = new SessionLogService(dataStore);
            var netService = new NetworkDiagnosticsService(logService);

            // Act
            var result = await netService.RunNetworkDiagnosticsAsync("127.0.0.1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("127.0.0.1", result.ServerTarget);
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
