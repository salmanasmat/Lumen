using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class ServicesTunerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Services Tuner";
}
