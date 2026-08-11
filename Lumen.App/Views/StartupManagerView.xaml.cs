using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class StartupManagerView : Page
{
    public StartupManagerView(StartupManagerViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
