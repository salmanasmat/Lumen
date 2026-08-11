using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class DashboardView : Page
{
    public DashboardView(DashboardViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
