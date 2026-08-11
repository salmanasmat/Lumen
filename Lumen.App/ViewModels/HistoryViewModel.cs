using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "History & Session Logs";
}
