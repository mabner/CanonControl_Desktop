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

    public MainWindowViewModel()
    {
        _cameraService = new CameraService();
    }

    [ObservableProperty]
    private string _status = "Disconnected";

    // Navigation State
    [ObservableProperty]
    private NavigationContext _currentContext = NavigationContext.RemoteCapture;

    [ObservableProperty]
    private ViewModelBase? _currentSidePanelViewModel;

    // Camera State
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleLiveViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ContextCaptureCommand))]
    private bool _isCameraConnected;

    [ObservableProperty]
    private string _cameraName = string.Empty;

    // Live View State
    [ObservableProperty]
    private Bitmap? _liveImage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FocusNearCommand))]
    [NotifyCanExecuteChangedFor(nameof(FocusFarCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    private bool _isLiveViewActive;

    // Camera Commands
    [RelayCommand]
    private void ConnectCamera()
    {
        var result = _cameraService.Connect();

        IsCameraConnected = result;

        if (result)
        {
            CameraName = _cameraService.GetCameraName();
            Status = "Connected";
        }
        else
        {
            Status = "Failed to connect";
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

    // Navigation Commands
    [RelayCommand]
    private void NavigateToRemoteCapture()
    {
        CurrentContext = NavigationContext.RemoteCapture;
        // TODO: Instantiate RemoteCaptureViewModel when it's created
        CurrentSidePanelViewModel = null; // Placeholder
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

    // Control Panel Commands (placeholders for later implementation)
    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private void ToggleLiveView() { }

    [RelayCommand(CanExecute = nameof(IsLiveViewActive))]
    private void FocusNear() { }

    [RelayCommand(CanExecute = nameof(IsLiveViewActive))]
    private void FocusFar() { }

    [RelayCommand(CanExecute = nameof(IsLiveViewActive))]
    private void AutoFocus() { }

    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private void ContextCapture() { }

    // Legacy window-opening commands (to be deprecated)
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
}
