using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class DiskCleanupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Disk Cleanup";
}
