using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class NetworkDiagnosticsView : Page
{
    public NetworkDiagnosticsView(NetworkDiagnosticsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
