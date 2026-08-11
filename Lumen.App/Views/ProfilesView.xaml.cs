using System.Windows.Controls;
using Lumen.App.ViewModels;

namespace Lumen.App.Views;

public partial class ProfilesView : Page
{
    public ProfilesView(ProfilesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
