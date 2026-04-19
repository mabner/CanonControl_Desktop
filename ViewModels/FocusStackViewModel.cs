using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.Services;

namespace CanonControl.ViewModels;

public partial class FocusStackViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    public FocusStackViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }
}
