using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Gui.Formatters.Thin;
using Gui.Models;
using Gui.Services.Thin;
using Gui.ViewModels;
using Gui.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // If you use CommunityToolkit, line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        // Register all the services needed for the application to run
        var collection = new ServiceCollection();
        const bool thin = false;
        if (thin)
        {
            collection.AddTransient<IRipperClient>();
            collection.AddTransient<IRipperSettings>();
            collection.AddTransient<IGuiSettings>();
            
            collection.AddSingleton(new HttpClient());
        
            collection.AddSingleton<LogEntryFormatter>();
            collection.AddSingleton<ILogTextService>(sp => new LogTextService(
                sp.GetRequiredService<LogEntryFormatter>(),
                maxEntries: 500));
        
            collection.AddSingleton<IBackendConnector, BackendConnector>();
            collection.AddSingleton<LogStreamCoordinator>();
        
            collection.AddSingleton<MainWindowViewModel>();
        
            collection.AddSingleton<ApplicationState>();
        }
        else
        {
            collection.AddTransient<IRipperClient>();
            collection.AddTransient<IRipperSettings>();
            collection.AddTransient<IGuiSettings>();
        }
        
        collection.AddTransient<MainWindowViewModel>();

        // Creates a ServiceProvider containing services from the provided IServiceCollection
        var services = collection.BuildServiceProvider();

        // Force event wiring to happen.
        _ = services.GetRequiredService<LogStreamCoordinator>();
        
        var vm = services.GetRequiredService<MainWindowViewModel>();
        
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow(vm);
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                singleViewPlatform.MainView = new MainWindow(vm);
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}