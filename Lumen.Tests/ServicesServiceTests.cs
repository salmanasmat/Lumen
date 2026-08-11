using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class ServicesServiceTests
{
    [Fact]
    public async Task GetServicesAsync_ReturnsServicesListAndProtectsNeverTouchItems()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_svc_{System.Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var restoreService = new RestorePointService();
            var logService = new SessionLogService(dataStore);
            var servicesService = new ServicesService(restoreService, logService);

            // Act
            var services = await servicesService.GetServicesAsync();

            // Assert
            Assert.NotNull(services);
            Assert.NotEmpty(services);

            // Verify NEVER_TOUCH list contains protected items
            Assert.True(ServicesService.NeverTouchList.Contains("Dhcp"));
            Assert.True(ServicesService.NeverTouchList.Contains("wuauserv"));
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
