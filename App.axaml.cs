using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CanonControl.Services;
using CanonControl.ViewModels;
using CanonControl.Views;

namespace CanonControl;

public partial class App : Application
{
    // singleton CameraService instance shared across the application
    private static CameraService? _cameraService;

    public static CameraService CameraService
    {
        get
        {
            _cameraService ??= new CameraService();
            return _cameraService;
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(CameraService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
