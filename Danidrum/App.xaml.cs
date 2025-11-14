using System.Windows;
using Danidrum.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Danidrum;
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<TrackListService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<PlaybackService>();

        Services = services.BuildServiceProvider();
    }
}

