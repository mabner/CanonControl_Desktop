using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CanonControl.Services;
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
    private async Task StartLiveView()
    {
        if (IsLiveViewRunning)
            return;

        IsLiveViewRunning = true;

        await _cameraService.StartLiveViewAsync(
            (frame) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    using var ms = new MemoryStream(frame);
                    LiveImage = new Bitmap(ms);
                });
            }
        );

        IsLiveViewRunning = false;
    }

    [RelayCommand]
    private void StopLiveView()
    {
        _cameraService.StopLiveView();
        Status = "Live View stopped";
    }
}
