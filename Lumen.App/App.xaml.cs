using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;
using Lumen.Core.Data;
using Lumen.Core.Interfaces;
using Lumen.App.ViewModels;
using Lumen.App.Views;

namespace Lumen.App;

public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    public static IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply WPF-UI Light Theme globally
        ApplicationThemeManager.Apply(ApplicationTheme.Light);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize SQLite DataStore
        var dataStore = _serviceProvider.GetRequiredService<SqliteDataStore>();
        await dataStore.InitializeAsync();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core Data & Infrastructure
        services.AddSingleton<SqliteDataStore>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StartupManagerViewModel>();
        services.AddTransient<BloatwareRemovalViewModel>();
        services.AddTransient<DiskCleanupViewModel>();
        services.AddTransient<ServicesTunerViewModel>();
        services.AddTransient<NetworkDiagnosticsViewModel>();
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<StartupManagerView>();
        services.AddTransient<BloatwareRemovalView>();
        services.AddTransient<DiskCleanupView>();
        services.AddTransient<ServicesTunerView>();
        services.AddTransient<NetworkDiagnosticsView>();
        services.AddTransient<ProfilesView>();
        services.AddTransient<HistoryView>();
        services.AddTransient<SettingsView>();
    }
}
