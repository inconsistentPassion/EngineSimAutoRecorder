using System.IO;
using System.Windows;
using EngineSimRecorder.View;
using EngineSimRecorder.View.Pages;
using EngineSimRecorder.ViewModel;
using EngineSimRecorder.ViewModel.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace EngineSimRecorder;

public partial class App : Application
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // WPF-UI services
            services.AddNavigationViewPageProvider();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();

            // Main window
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();

            // Pages + ViewModels
            services.AddTransient<RecorderPage>();
            services.AddTransient<RecorderPageViewModel>();
            services.AddTransient<LogPage>();
            services.AddTransient<LogPageViewModel>();
            services.AddTransient<OptionsPage>();
            services.AddTransient<OptionsPageViewModel>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    public static T GetService<T>() where T : class => _host.Services.GetRequiredService<T>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
