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
    private Bitmap? _liveImage;

    public LiveViewViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    #region Live View

    [RelayCommand]
    private async Task StartLiveView()
    {
        await _cameraService.StartEvfAsync(
            (frame) =>
            {
                // UI can only be updated from the main thread, so we need to dispatch the update
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
        _cameraService.EndEvf();
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
    private void StartAutoFocus()
    {
        _cameraService.StartAutoFocus();
    }

    [RelayCommand]
    private void StopAutoFocus()
    {
        _cameraService.StopAutoFocus();
    }

    [RelayCommand]
    private void StartFocusNear()
    {
        _cameraService.StartFocusNear();
    }

    [RelayCommand]
    private void StartFocusFar()
    {
        _cameraService.StartFocusFar();
    }

    [RelayCommand]
    private void StopFocus()
    {
        _cameraService.StopFocus();
    }

    [RelayCommand]
    private async Task TakePicture()
    {
        await Task.Run(() => _cameraService.TakePicture());
    }

    #endregion Focus Control
}
