using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EngineSimRecorder;
using EngineSimRecorder.Services;
using EngineSimRecorder.ViewModel;
using EngineSimRecorder.View.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace EngineSimRecorder.View;

public partial class MainWindow : FluentWindow, INavigationWindow
{
    public MainWindowViewModel ViewModel { get; }

    // Focus monitor
    internal static IntPtr EngineSimHwnd { get; set; } = IntPtr.Zero;
    private static DispatcherTimer? _focusMonitor;
    private static bool _focusWarned = false;

    public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService, ISnackbarService snackbarService, IContentDialogService dialogService)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();

        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(AppSnackbar);
        dialogService.SetDialogHost(RootDialog);

        Application.Current.MainWindow = this;
        Loaded += (s, e) =>
        {
            Activate();
            WireAutomationNavOpenPaneOnClick();
        };
        Closing += OnClosing;
    }

    /// <summary>
    /// WPF-UI only toggles submenu expansion when the pane is open; if the nav is collapsed, open it and expand Automation.
    /// </summary>
    private void WireAutomationNavOpenPaneOnClick()
    {
        foreach (object item in ViewModel.MenuItems)
        {
            if (item is not NavigationViewItem nvi) continue;
            if (!MainWindowViewModel.AutomationNavigationRootTag.Equals(nvi.Tag as string)) continue;

            nvi.Click += (_, _) =>
            {
                if (!RootNavigation.IsPaneOpen)
                {
                    RootNavigation.IsPaneOpen = true;
                    nvi.IsExpanded = true;
                }
            };
            return;
        }
    }

    // ── Focus Monitor ──
    internal static void StartFocusMonitor(RecorderPage page)
    {
        _focusWarned = false;
        _focusMonitor?.Stop();
        _focusMonitor = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _focusMonitor.Tick += FocusMonitor_Tick;
        _focusMonitor.Start();
    }

    internal static void StopFocusMonitor()
    {
        _focusMonitor?.Stop();
        _focusMonitor = null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static void FocusMonitor_Tick(object? sender, EventArgs e)
    {
        if (EngineSimHwnd == IntPtr.Zero || RecorderPage.Instance == null) return;
        
        IntPtr focused = GetForegroundWindow();
        IntPtr mainWindowHandle = new WindowInteropHelper(Application.Current.MainWindow!).Handle;
        
        bool isFocused = (focused == mainWindowHandle) || (focused == EngineSimHwnd);
        var page = RecorderPage.Instance;

        if (!isFocused && !_focusWarned)
        {
            _focusWarned = true;
            page.lblStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
        }
        else if (isFocused && _focusWarned)
        {
            _focusWarned = false;
            page.lblStatus.Foreground = Brushes.Gray;
        }
    }

    // ── Closing ──
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        RecorderPage.Instance?.Cts?.Cancel();
        RecorderPage.Instance?.Backend?.Dispose();
        OptionsPage.Instance?.SaveSettings();
        if (App.Services.GetService(typeof(AutomationService)) is AutomationService automation)
        {
            automation.SaveToSettings();
            automation.Disconnect();
        }
    }

    public INavigationView GetNavigation() => RootNavigation;
    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
    public void SetServiceProvider(IServiceProvider serviceProvider) { }
    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    public void ShowWindow() => Show();
    public void CloseWindow() => Close();
}
