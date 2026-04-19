using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CanonControl.Models;
using CanonControl.Services;
using CanonControl.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;
    private readonly SettingsService _settingsService;

    public MainWindowViewModel()
    {
        _cameraService = new CameraService();
        _settingsService = new SettingsService();

        // load and apply settings
        LoadSettings();
    }

    [ObservableProperty]
    private string _status = "Disconnected";

    // navigation state
    [ObservableProperty]
    private NavigationContext _currentContext = NavigationContext.RemoteCapture;

    [ObservableProperty]
    private ViewModelBase? _currentSidePanelViewModel;

    // camera state
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleLiveViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ContextCaptureCommand))]
    private bool _isCameraConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _cameraName = string.Empty;

    // computed property for window title
    public string WindowTitle =>
        string.IsNullOrEmpty(CameraName) ? "CanonControl" : $"CanonControl - {CameraName}";

    // live View State
    [ObservableProperty]
    private Bitmap? _liveImage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(StartFocusNearCommand),
        nameof(StartFocusFarCommand),
        nameof(StopFocusCommand),
        nameof(StartAutoFocusCommand),
        nameof(StopAutoFocusCommand)
    )]
    private bool _isLiveViewActive;

    // camera commands
    [RelayCommand]
    private async Task ConnectCamera()
    {
        Status = "Connecting...";
        // load current settings to get connection timeout
        var settings = _settingsService.Load();

        var result = await _cameraService.ConnectAsync(settings.ConnectionTimeout);

        IsCameraConnected = result;

        if (result)
        {
            CameraName = _cameraService.GetCameraName();
            Status = "Connected";
        }
        else
        {
            Status = "Failed to connect";
            await ShowErrorDialogAsync(
                "Camera Connection Failed",
                "No Canon camera detected. Please connect a camera and try again."
            );
        }
    }

    [RelayCommand]
    private void DisconnectCamera()
    {
        _cameraService.Disconnect();

        IsCameraConnected = false;
        CameraName = string.Empty;
        Status = "Disconnected";
    }

    // navigation commands
    [RelayCommand]
    private void NavigateToRemoteCapture()
    {
        CurrentContext = NavigationContext.RemoteCapture;
        CurrentSidePanelViewModel = new RemoteCaptureViewModel(_cameraService);
    }

    [RelayCommand]
    private void NavigateToFocusStack()
    {
        CurrentContext = NavigationContext.FocusStack;
        CurrentSidePanelViewModel = new FocusStackViewModel(_cameraService);
    }

    [RelayCommand]
    private void NavigateToTimeLapse()
    {
        CurrentContext = NavigationContext.TimeLapse;
        CurrentSidePanelViewModel = new TimeLapseViewModel(_cameraService);
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentContext = NavigationContext.Settings;
        CurrentSidePanelViewModel = new SettingsViewModel();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        // apply settings to CameraService
        _cameraService.LiveViewDuringAutoFocus = settings.LiveViewDuringAutoFocus;

        // TODO: apply other settings (LiveViewFrameRate, etc.) when those features are implemented
    }

    // control panel commands
    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private async Task ToggleLiveView()
    {
        if (!IsLiveViewActive)
        {
            // start live view
            try
            {
                // start the live view task (it runs in background)
                var liveViewTask = _cameraService.StartLiveViewAsync(frameData =>
                {
                    // convert byte[] to Bitmap and update LiveImage on UI thread
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            using var stream = new System.IO.MemoryStream(frameData);
                            LiveImage = new Bitmap(stream);
                        }
                        catch
                        {
                            // silently handle bitmap creation errors
                        }
                    });
                });

                IsLiveViewActive = true;

                // explicitly notify focus commands to re-evaluate CanExecute
                StartFocusNearCommand.NotifyCanExecuteChanged();
                StartFocusFarCommand.NotifyCanExecuteChanged();
                StopFocusCommand.NotifyCanExecuteChanged();
                StartAutoFocusCommand.NotifyCanExecuteChanged();
                StopAutoFocusCommand.NotifyCanExecuteChanged();

                // don't await the live view task - it runs continuously until stopped
            }
            catch
            {
                // silently handle errors - keep toggle in current state
                IsLiveViewActive = false;
            }
        }
        else
        {
            // stop live view
            try
            {
                _cameraService.StopLiveView();
                IsLiveViewActive = false;
                LiveImage = null;

                // explicitly notify focus commands to re-evaluate CanExecute
                StartFocusNearCommand.NotifyCanExecuteChanged();
                StartFocusFarCommand.NotifyCanExecuteChanged();
                StopFocusCommand.NotifyCanExecuteChanged();
                StartAutoFocusCommand.NotifyCanExecuteChanged();
                StopAutoFocusCommand.NotifyCanExecuteChanged();
            }
            catch
            {
                // silently handle errors - keep toggle in current state
            }
        }
    }

    private bool CanExecuteFocusCommands() => IsLiveViewActive;

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StartFocusNear()
    {
        _cameraService.StartFocusNear();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StartFocusFar()
    {
        _cameraService.StartFocusFar();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StopFocus()
    {
        _cameraService.StopFocus();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StartAutoFocus()
    {
        _cameraService.StartAutoFocus();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StopAutoFocus()
    {
        _cameraService.StopAutoFocus();
    }

    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private void ContextCapture() { }

    // legacy window-opening commands (to be deprecated)
    [RelayCommand]
    private void OpenLiveView()
    {
        // share conection in the same session
        var window = new LiveViewWindow(new LiveViewViewModel(_cameraService));

        window.Show();
    }

    [RelayCommand]
    private void OpenFocusStack()
    {
        var window = new FocusStackWindow { DataContext = new FocusStackViewModel(_cameraService) };

        window.Show();
    }

    [RelayCommand]
    private void OpenTimeLapse()
    {
        var window = new TimeLapseWindow { DataContext = new TimeLapseViewModel(_cameraService) };

        window.Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new SettingsWindow { DataContext = new SettingsViewModel() };

        window.Show();
    }

    // helper method to show error dialogs
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        if (
            Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                var dialog = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 200,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Spacing = 20,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            },
                            new Button
                            {
                                Content = "OK",
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                MinWidth = 100,
                                MinHeight = 44,
                            },
                        },
                    },
                };

                // wire up the OK button to close the dialog
                if (dialog.Content is StackPanel panel && panel.Children[1] is Button okButton)
                {
                    okButton.Click += (s, e) => dialog.Close();
                }

                await dialog.ShowDialog(mainWindow);
            }
        }
    }
}
