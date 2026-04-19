using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class TimeLapseViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    public TimeLapseViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }
}
