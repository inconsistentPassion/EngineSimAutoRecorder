using System;
using System.Windows;
using EngineSimRecorder.ViewModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
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
    }

    public INavigationView GetNavigation() => RootNavigation;
    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
    public void SetServiceProvider(IServiceProvider serviceProvider) { }
    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    public void ShowWindow() => Show();
    public void CloseWindow() => Close();
}
