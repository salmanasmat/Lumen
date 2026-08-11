using System;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Wpf.Ui.Controls;
using Lumen.Core.Data;
using Lumen.Core.Interfaces;
using Lumen.Core.Services;
using Lumen.App.Converters;
using Lumen.App.ViewModels;
using Lumen.App.Views;

namespace Lumen.Tests;

public class NavigationViewInspectionTests
{
    private readonly ITestOutputHelper _output;

    public NavigationViewInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestAllViewsInstantiationInStaThread()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new Application();
                    app.Resources.Add("BooleanToVisibilityConverter", new System.Windows.Controls.BooleanToVisibilityConverter());
                    app.Resources.Add("InverseBooleanToVisibilityConverter", new InverseBooleanToVisibilityConverter());
                    app.Resources.Add("InverseBooleanConverter", new InverseBooleanConverter());
                }

                var services = new ServiceCollection();
                services.AddSingleton<SqliteDataStore>(sp => new SqliteDataStore(":memory:"));
                services.AddSingleton<IRestorePointService, RestorePointService>();
                services.AddSingleton<ISessionLogService, SessionLogService>();
                services.AddSingleton<IUndoService, UndoService>();
                services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<IBloatwareService, BloatwareService>();
                services.AddSingleton<IDiskCleanupService, DiskCleanupService>();
                services.AddSingleton<IServicesService, ServicesService>();
                services.AddSingleton<INetworkDiagnosticsService, NetworkDiagnosticsService>();
                services.AddSingleton<IProfileService, ProfileService>();

                services.AddTransient<DashboardViewModel>();
                services.AddTransient<StartupManagerViewModel>();
                services.AddTransient<BloatwareRemovalViewModel>();
                services.AddTransient<DiskCleanupViewModel>();
                services.AddTransient<ServicesTunerViewModel>();
                services.AddTransient<NetworkDiagnosticsViewModel>();
                services.AddTransient<ProfilesViewModel>();
                services.AddTransient<HistoryViewModel>();
                services.AddTransient<SettingsViewModel>();

                services.AddTransient<DashboardView>();
                services.AddTransient<StartupManagerView>();
                services.AddTransient<BloatwareRemovalView>();
                services.AddTransient<DiskCleanupView>();
                services.AddTransient<ServicesTunerView>();
                services.AddTransient<NetworkDiagnosticsView>();
                services.AddTransient<ProfilesView>();
                services.AddTransient<HistoryView>();
                services.AddTransient<SettingsView>();

                var provider = services.BuildServiceProvider();

                var viewsToTest = new Type[]
                {
                    typeof(DashboardView),
                    typeof(StartupManagerView),
                    typeof(BloatwareRemovalView),
                    typeof(DiskCleanupView),
                    typeof(ServicesTunerView),
                    typeof(NetworkDiagnosticsView),
                    typeof(ProfilesView),
                    typeof(HistoryView),
                    typeof(SettingsView)
                };

                foreach (var viewType in viewsToTest)
                {
                    var instance = provider.GetRequiredService(viewType);
                    Assert.NotNull(instance);
                    _output.WriteLine($"Successfully instantiated {viewType.Name}!");
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }
}
