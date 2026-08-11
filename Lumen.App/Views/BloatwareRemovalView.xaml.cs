using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class BloatwareRemovalView : Page
{
    public BloatwareRemovalView(BloatwareRemovalViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
