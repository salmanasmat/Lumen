using System.Windows;
using Wpf.Ui.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel, INavigationViewPageProvider pageProvider)
    {
        DataContext = viewModel;
        InitializeComponent();

        RootNavigation.SetPageService(pageProvider);

        Loaded += (s, e) =>
        {
            RootNavigation.Navigate(typeof(DashboardView));
        };
    }
}
