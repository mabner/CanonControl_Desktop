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

    #region Live View

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
    #endregion Live View

    #region Focus Control

    [RelayCommand]
    private async Task FocusNear()
    {
        await Task.Run(() => _cameraService.FocusNearMedium());
    }

    [RelayCommand]
    private async Task FocusFar()
    {
        await Task.Run(() => _cameraService.FocusFarMedium());
    }

    [RelayCommand]
    private async Task AutoFocus()
    {
        await Task.Run(() => _cameraService.AutoFocus());
    }

    [RelayCommand]
    private async Task TakePicture()
    {
        await Task.Run(() => _cameraService.TakePicture());
    }

    public void FocusAtPoint(double x, double y)
    {
        // inicialmente só chama AF
        _cameraService.AutoFocus();
    }

    #endregion Focus Control
}
