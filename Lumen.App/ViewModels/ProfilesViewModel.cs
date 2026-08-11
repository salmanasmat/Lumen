using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Optimization Profiles";
}
