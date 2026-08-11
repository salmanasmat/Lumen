using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class DiskCleanupServiceTests
{
    [Fact]
    public async Task CalculateReclaimableSizesAsync_ReturnsCategoriesList()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_disk_{System.Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var restoreService = new RestorePointService();
            var logService = new SessionLogService(dataStore);
            var diskService = new DiskCleanupService(restoreService, logService);

            // Act
            var categories = await diskService.CalculateReclaimableSizesAsync();

            // Assert
            Assert.NotNull(categories);
            Assert.NotEmpty(categories);
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
