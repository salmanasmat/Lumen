using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Lumen.Core.Data;
using Lumen.Core.Interfaces;
using Lumen.Core.Services;
using Lumen.App.Services;
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

        // Register Global Unhandled Exception Handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = true;
            MessageBox.Show($"An unexpected error occurred:\n{args.Exception.Message}\n\nDetails logged to %LocalAppData%\\Lumen\\crash.log", "Lumen Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // Apply WPF-UI Light Theme globally
        ApplicationThemeManager.Apply(ApplicationTheme.Light);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize SQLite DataStore
        var dataStore = _serviceProvider.GetRequiredService<SqliteDataStore>();
        await dataStore.InitializeAsync();

        // Check for CLI Silent Mode: Lumen.exe --profile "<name>" --silent
        if (e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase))
        {
            await HandleSilentExecutionAsync(e.Args);
            Shutdown(0);
            return;
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private async Task HandleSilentExecutionAsync(string[] args)
    {
        try
        {
            string profileName = "Office Terminal Workstation";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--profile", StringComparison.OrdinalIgnoreCase))
                {
                    profileName = args[i + 1].Trim('"');
                    break;
                }
            }

            var profileService = _serviceProvider!.GetRequiredService<IProfileService>();
            var profile = await profileService.GetDefaultProfileAsync();

            var logsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "Logs");
            Directory.CreateDirectory(logsFolder);
            var logPath = Path.Combine(logsFolder, $"{DateTime.Now:yyyyMMdd_HHmmss}-silent-run.log");

            await File.AppendAllTextAsync(logPath, $"[{DateTime.Now}] Starting Silent Fleet Optimization for profile '{profileName}'...\n");

            var progress = new Progress<string>(msg =>
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] {msg}\n");
            });

            var (success, resultMsg) = await profileService.ApplyProfileAsync(profile, progress);
            await File.AppendAllTextAsync(logPath, $"[{DateTime.Now}] Finished with result: Success={success}, Message={resultMsg}\n");
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            Environment.ExitCode = 1;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // WPF-UI Infrastructure
        services.AddSingleton<INavigationViewPageProvider, PageService>();

        // Core Services
        services.AddSingleton<SqliteDataStore>();
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

        // Views (Pages)
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

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash(ex);
        }
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");
            Directory.CreateDirectory(folder);
            var logPath = Path.Combine(folder, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] UNHANDLED EXCEPTION:\n{ex}\n\n");
        }
        catch { }
    }
}
