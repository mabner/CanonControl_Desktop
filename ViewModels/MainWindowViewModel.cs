using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _status = "Camera not connected";

    private readonly CameraService _cameraService;

    public MainWindowViewModel()
    {
        _cameraService = new CameraService();
    }

    [RelayCommand]
    private void ConnectCamera()
    {
        var result = _cameraService.Connect();
        Status = result ? "Camera connected" : "Failed to connect";
    }

    [RelayCommand]
    private void StartLiveView()
    {
        _cameraService.StartLiveView();
        Status = "Live View started";
    }
}
