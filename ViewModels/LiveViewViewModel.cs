using CommunityToolkit.Mvvm.ComponentModel;
using CanonControl.Services;

namespace CanonControl.ViewModels;

public partial class LiveViewViewModel : ObservableObject
{
    private readonly CameraService _cameraService;

    public LiveViewViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }
}