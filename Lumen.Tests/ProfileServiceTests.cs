using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task GetDefaultProfileAsync_ReturnsEmbeddedWorkstationProfile()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_prof_{System.Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var restoreService = new RestorePointService();
            var logService = new SessionLogService(dataStore);
            var startupService = new StartupService(restoreService, logService);
            var bloatwareService = new BloatwareService(restoreService, logService);
            var diskService = new DiskCleanupService(restoreService, logService);
            var servicesService = new ServicesService(restoreService, logService);

            var profileService = new ProfileService(
                startupService,
                bloatwareService,
                diskService,
                servicesService,
                restoreService,
                logService);

            // Act
            var profile = await profileService.GetDefaultProfileAsync();

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("Office Terminal Workstation", profile.Name);
            Assert.NotEmpty(profile.DisabledServices);
            Assert.NotEmpty(profile.RemovedBloatwarePackages);
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
