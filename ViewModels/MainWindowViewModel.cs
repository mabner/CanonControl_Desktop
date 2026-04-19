using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _status = "Camera not connected";

    [ObservableProperty]
    private Bitmap _liveImage;

    private readonly CameraService _cameraService;

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
        await _cameraService.StartLiveViewAsync(
            (frame) =>
            {
                using var ms = new MemoryStream(frame);
                LiveImage = new Bitmap(ms);
            }
        );
    }
}
