using CommunityToolkit.Mvvm.ComponentModel;

namespace EngineSimRecorder.ViewModel.Pages;

public partial class RecorderPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Idle...";

    public void Initialize(View.Pages.RecorderPage page)
    {
        // Wire up all button events here
    }
}
