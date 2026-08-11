using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class ServicesTunerView : Page
{
    public ServicesTunerView(ServicesTunerViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
