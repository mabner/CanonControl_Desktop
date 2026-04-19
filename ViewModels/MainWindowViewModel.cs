using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
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

    [ObservableProperty]
    private bool _isCameraConnected;

    [ObservableProperty]
    private string _cameraName = string.Empty;

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
    private void OpenLiveView()
    {
        var window = new LiveViewWindow { DataContext = new LiveViewViewModel(_cameraService) };

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
