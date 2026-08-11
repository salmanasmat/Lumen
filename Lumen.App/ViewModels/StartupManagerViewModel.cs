using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class StartupManagerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Startup Manager";
}
