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
        CreateAutomationNav(),
    };

    [ObservableProperty]
    private ObservableCollection<object> _footerMenuItems = new()
    {
        new NavigationViewItem("Options", SymbolRegular.Settings24, typeof(OptionsPage)),
    };

    /// <summary>Tag on the Automation root item (see <c>MainWindow</c> pane wiring).</summary>
    internal const string AutomationNavigationRootTag = "AutomationNavRoot";

    private static NavigationViewItem CreateAutomationNav()
    {
        var root = new NavigationViewItem
        {
            Content = "Automation",
            Icon = new SymbolIcon { Symbol = SymbolRegular.Bot24 },
            IsExpanded = true,
            Tag = AutomationNavigationRootTag,
        };
        root.MenuItems.Add(new NavigationViewItem("Workspace", SymbolRegular.Folder24, typeof(AutomationConfigPage)));
        root.MenuItems.Add(new NavigationViewItem("Execution", SymbolRegular.Play24, typeof(AutomationScriptPage)));
        root.MenuItems.Add(new NavigationViewItem("Deployment", SymbolRegular.Box24, typeof(AutomationPackagePage)));
        return root;
    }
}
