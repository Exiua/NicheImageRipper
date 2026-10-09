using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NicheImageRipper.Gui.Formatters.Thin;
using NicheImageRipper.Gui.Services;
using NicheImageRipper.Gui.Services.Full;
using NicheImageRipper.Gui.Services.Shared;
using NicheImageRipper.Gui.Services.Shared.Windows;
using NicheImageRipper.Gui.Services.Thin;
using NicheImageRipper.Gui.ViewModels;
using NicheImageRipper.Gui.Views;
using Serilog;

namespace NicheImageRipper.Gui;

public partial class App : Application
{
    public static bool Thin { get; set; } = false;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Register all the services needed for the application to run
        var collection = new ServiceCollection();
        if (Thin)
        {
            collection.AddSingleton(new HttpClient());
        
            collection.AddSingleton<LogEntryFormatter>();
        
            collection.AddSingleton<IBackendConnector, BackendConnector>();
        
            collection.AddSingleton<ApplicationState>();
            
            collection.AddSingleton<IRipperClient, RemoteRipperClient>();
            collection.AddSingleton<IRipperSettings, ThinRipperSettings>();
            collection.AddSingleton<IGuiSettings, ThinGuiSettings>();
            collection.AddSingleton<IGuiLogBridgeCoordinator, RemoteGuiLogBridgeCoordinator>();
            collection.AddSingleton<MainWindowViewModelBase, MainWindowViewModelThin>();
        }
        else
        {
            collection.AddSingleton<IRipperClient, LocalRipperClient>();
            collection.AddSingleton<IRipperSettings, FullRipperSettings>();
            collection.AddSingleton<IGuiSettings, FullGuiSettings>();
            collection.AddSingleton<IGuiLogBridgeCoordinator, LocalGuiLogBridgeCoordinator>();
            collection.AddSingleton<MainWindowViewModelBase, MainWindowViewModelFull>();
        }
        
        collection.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(Log.Logger, dispose: true);
        });
        
        collection.AddSingleton<ILogTextSource, RollingLogTextSource>();
        
        #if WINDOWS
        collection.AddSingleton<ITaskbarProgressService, WindowsTaskbarProgressService>();
        #else
        collection.AddSingleton<ITaskbarProgressService, NullTaskbarProgressService>();
        #endif

        // Creates a ServiceProvider containing services from the provided IServiceCollection
        var services = collection.BuildServiceProvider();

        // Force event wiring to happen.
        _ = services.GetRequiredService<IGuiLogBridgeCoordinator>();
        
        var vm = services.GetRequiredService<MainWindowViewModelBase>();
        var taskbarProgressService = services.GetRequiredService<ITaskbarProgressService>();
        var logger = services.GetRequiredService<ILogger<MainWindow>>();
        
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
            {
                desktop.MainWindow = new MainWindow(vm, taskbarProgressService, logger);
                break;
            }
            case ISingleViewApplicationLifetime singleViewPlatform:
            {
                singleViewPlatform.MainView = new MainWindow(vm, taskbarProgressService, logger);
                break;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}