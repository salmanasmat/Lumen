using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _applicationTitle = "Lumen — PC Health & Debloat Toolkit";
}
