using Wpf.Ui.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
