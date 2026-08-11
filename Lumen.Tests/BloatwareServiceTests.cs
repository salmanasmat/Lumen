using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class BloatwareServiceTests
{
    [Fact]
    public async Task GetInstalledBloatwareAsync_ReturnsCuratedPackageList()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_bloat_{System.Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var restoreService = new RestorePointService();
            var logService = new SessionLogService(dataStore);
            var bloatwareService = new BloatwareService(restoreService, logService);

            // Act
            var packages = await bloatwareService.GetInstalledBloatwareAsync();

            // Assert
            Assert.NotNull(packages);
            Assert.NotEmpty(packages);
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
