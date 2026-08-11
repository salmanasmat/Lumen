using System;
using System.Windows;
using Wpf.Ui.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider)
    {
        DataContext = viewModel;
        InitializeComponent();

        RootNavigation.SetServiceProvider(serviceProvider);

        Loaded += (s, e) =>
        {
            RootNavigation.Navigate(typeof(DashboardView));
        };
    }
}
