using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class NetworkDiagnosticsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Network & Logon Diagnostics";
}
