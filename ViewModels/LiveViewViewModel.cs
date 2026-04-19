using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class LiveViewViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    public LiveViewViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }
}
