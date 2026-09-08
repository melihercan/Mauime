using DemoApp.ViewModels;
using Mauime.Configuration;
using Mauime.Hosting;
using Mauime.Nfc;
using Mauime.WebHostPatch;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReactiveUI.Builder;
using ReactiveUI.Primitives.Concurrency;
using Syncfusion.Maui.Toolkit.Hosting;

namespace DemoApp;

/// <summary>
/// Wires up all four Mauime libraries. This file is the point of the demo: everything each package
/// asks of an app is here, in one place, and it is short.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // ReactiveUI 24 must be initialized explicitly before anything touches WhenAnyValue;
        // without this the first view model throws "ReactiveUI has not been initialized". Earlier
        // versions self-initialized from a static, which is why no MAUI sample mentions it.
        // Platform services first: WithCoreServices returns Splat's IAppBuilder, which has no
        // WithPlatformServices, so the order ReactiveUI's own error message suggests does not
        // compile.
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithPlatformServices()
            .WithCoreServices()
            .BuildApp();

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Mauime.Configuration - appsettings.json ships in the app package as a MauiAsset.
        builder.Configuration.AddAppPackageJson();

        // Mauime.Hosting - the environment name comes from MAUI_ENVIRONMENT in that file, read when
        // IHostEnvironment is first resolved. Xamarinme called the key XAMARIN_ENVIRONMENT.
        builder.UseMauimeHostingFromConfiguration();

        // ...and then the overlay for whichever environment it named. Two calls rather than one,
        // because the file that says which environment this is has to be read before the
        // environment-specific file can be chosen.
        var environment = builder.Configuration[MauimeHostingExtensions.DefaultConfigurationKey];
        if (!string.IsNullOrEmpty(environment))
        {
            builder.Configuration.AddAppPackageJson(
                MauimeConfigurationExtensions.EnvironmentFileName(
                    MauimeConfigurationExtensions.DefaultFileName, environment));
        }

        // Mauime.Nfc - registers INfc, and wires Android's OnNewIntent so MainActivity needs no edit.
        builder.UseMauimeNfc();

        // Mauime.WebHostPatch - registers the server but does not start it. Starting a listening
        // socket the moment an app launches is rarely what anyone wants.
        builder.UseMauimeWebHost(options =>
        {
            options.Port = int.TryParse(builder.Configuration["WebHost:Port"], out var port) ? port : 0;
            options.ConfigureApplication = app => app.Run(context =>
                context.Response.WriteAsync("Hello from a .NET MAUI app, served by Kestrel."));
        });

        // ReactiveUI 24 has no RxApp.MainThreadScheduler, so the UI-thread sequencer is supplied
        // explicitly. Resolved lazily, on the thread that first asks for a view model, which is the
        // UI thread.
        builder.Services.AddSingleton<ISequencer>(_ =>
            new SynchronizationContextSequencer(SynchronizationContext.Current ?? new SynchronizationContext()));

        builder.Services.AddSingleton<ConfigurationViewModel>();
        builder.Services.AddSingleton<HostingViewModel>();
        builder.Services.AddSingleton<NfcViewModel>();
        builder.Services.AddSingleton<WebHostViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
