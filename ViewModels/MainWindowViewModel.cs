using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    # region Properties

    [ObservableProperty]
    private string _status = "Camera not connected";

    [ObservableProperty]
    private Bitmap _liveImage;

    [ObservableProperty]
    private bool _isLiveViewRunning;

    private readonly CameraService _cameraService;

    #endregion Properties


    public MainWindowViewModel()
    {
        _cameraService = new CameraService();
    }

    [RelayCommand]
    private void ConnectCamera()
    {
        var result = _cameraService.Connect();

        if (result)
            Status = $"Connected: {_cameraService.GetCameraName()}";
        else
            Status = "Failed to connect";
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
