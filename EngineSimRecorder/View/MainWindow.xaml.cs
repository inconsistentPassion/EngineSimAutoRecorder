using System;
using System.Windows;
using EngineSimRecorder.ViewModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace EngineSimRecorder.View;

public partial class MainWindow : FluentWindow, INavigationWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService, ISnackbarService snackbarService)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        InitializeComponent();

        navigationService.SetNavigationControl(RootNavigation);

        Application.Current.MainWindow = this;
        Loaded += (s, e) => Activate();

        // Theme toggle
        UpdateThemeButton();
        btnTheme.Click += BtnTheme_Click;
    }

    private int _themeIndex = 0; // 0=Dark, 1=Light, 2=System

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        _themeIndex = (_themeIndex + 1) % 3;
        switch (_themeIndex)
        {
            case 0: ApplicationThemeManager.Apply(ApplicationTheme.Dark); break;
            case 1: ApplicationThemeManager.Apply(ApplicationTheme.Light); break;
            case 2: ApplicationThemeManager.Apply(ApplicationTheme.Unknown); break;
        }
        UpdateThemeButton();
    }

    private void UpdateThemeButton()
    {
        btnTheme.Content = _themeIndex switch
        {
            0 => "Dark",
            1 => "Light",
            _ => "System"
        };
    }

    public INavigationView GetNavigation() => RootNavigation;
    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
    public void SetServiceProvider(IServiceProvider serviceProvider) { }
    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    public void ShowWindow() => Show();
    public void CloseWindow() => Close();
}
