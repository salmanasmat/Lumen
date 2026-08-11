using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class DiskCleanupView : Page
{
    public DiskCleanupView(DiskCleanupViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
