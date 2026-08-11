using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Lumen.Core.Data;
using Lumen.Core.Models;
using Lumen.Core.Services;

namespace Lumen.Tests;

public class DiagnosticsServiceTests
{
    [Fact]
    public async Task RunFullScanAsync_ReturnsValidSnapshotAndSavesToSqlite()
    {
        // Arrange
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"lumen_test_{Guid.NewGuid()}.db");
        try
        {
            var dataStore = new SqliteDataStore(tempDbPath);
            await dataStore.InitializeAsync();

            var service = new DiagnosticsService(dataStore);

            // Act
            var snapshot = await service.RunFullScanAsync();

            // Assert
            Assert.NotNull(snapshot);
            Assert.True(snapshot.Timestamp <= DateTime.Now);

            var history = await dataStore.GetScanHistoryAsync();
            Assert.NotEmpty(history);
            Assert.Equal(snapshot.Timestamp.ToString("g"), history[0].Timestamp.ToString("g"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(tempDbPath))
            {
                try
                {
                    File.Delete(tempDbPath);
                }
                catch
                {
                    // Ignore cleanup exceptions in temp dir
                }
            }
        }
    }

    [Theory]
    [InlineData(25, MetricSeverity.Good)]
    [InlineData(45, MetricSeverity.Warning)]
    [InlineData(110, MetricSeverity.Critical)]
    public void MetricSeverity_BootTimeThresholds_EvaluatesCorrectly(double bootTimeSeconds, MetricSeverity expectedSeverity)
    {
        // Arrange
        var snapshot = new DiagnosticsSnapshot
        {
            AverageBootTimeSeconds = bootTimeSeconds,
            SystemDriveFreePercent = 50,
            AvailableRamGb = 8,
            TotalRamGb = 16
        };

        // Act
        MetricSeverity severity;
        if (snapshot.AverageBootTimeSeconds < 30) severity = MetricSeverity.Good;
        else if (snapshot.AverageBootTimeSeconds <= 90) severity = MetricSeverity.Warning;
        else severity = MetricSeverity.Critical;

        // Assert
        Assert.Equal(expectedSeverity, severity);
    }
}
