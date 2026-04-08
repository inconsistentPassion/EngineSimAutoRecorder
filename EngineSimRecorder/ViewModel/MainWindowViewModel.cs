using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EngineSimRecorder.View.Pages;
using Wpf.Ui.Controls;

namespace EngineSimRecorder.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _applicationTitle = "ES Recorder";

    [ObservableProperty]
    private ObservableCollection<object> _menuItems = new()
    {
        new NavigationViewItem("Recorder", SymbolRegular.Record24, typeof(RecorderPage)),
        new NavigationViewItem("Log", SymbolRegular.ClipboardCode24, typeof(LogPage)),
    };

    [ObservableProperty]
    private ObservableCollection<object> _footerMenuItems = new()
    {
        new NavigationViewItem("Options", SymbolRegular.Settings24, typeof(OptionsPage)),
    };
}
