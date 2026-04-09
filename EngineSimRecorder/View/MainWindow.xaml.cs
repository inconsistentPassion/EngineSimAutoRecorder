using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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

    public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService, ISnackbarService snackbarService)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();

        navigationService.SetNavigationControl(RootNavigation);

        Application.Current.MainWindow = this;
        Loaded += (s, e) => Activate();
        Closing += OnClosing;
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
        RecorderPage.Instance?._cts?.Cancel();
        RecorderPage.Instance?.Backend?.Dispose();
        OptionsPage.Instance?.SaveSettings();
    }

    public INavigationView GetNavigation() => RootNavigation;
    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
    public void SetServiceProvider(IServiceProvider serviceProvider) { }
    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    public void ShowWindow() => Show();
    public void CloseWindow() => Close();
}
