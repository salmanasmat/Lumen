using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class BloatwareRemovalViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Bloatware Removal";
}
