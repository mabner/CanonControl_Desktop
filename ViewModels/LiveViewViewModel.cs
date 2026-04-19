using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class LiveViewViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    [ObservableProperty]
    private Bitmap _liveImage;

    public LiveViewViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    [RelayCommand]
    private async Task StartLiveView()
    {
        await _cameraService.StartLiveViewAsync(
            (frame) =>
            {
                // UI só pode ser atualizada na thread principal
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    using var ms = new MemoryStream(frame);
                    LiveImage = new Bitmap(ms);
                });
            }
        );
    }

    [RelayCommand]
    private void StopLiveView()
    {
        _cameraService.StopLiveView();
    }
}
