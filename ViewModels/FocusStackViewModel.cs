using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class FocusStackViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    [ObservableProperty]
    private int _numberOfShots = 10;

    [ObservableProperty]
    private int _stepSize = 1;

    [ObservableProperty]
    private bool _isRunning;

    public FocusStackViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task StartStack()
    {
        if (IsRunning)
            return;

        IsRunning = true;

        try
        {
            // TODO: Implement focus stacking sequence
            // For each shot:
            //   1. Take picture via _cameraService.TakePicture()
            //   2. Move focus by StepSize via _cameraService.DriveLens()
            //   3. Wait for camera to be ready
            // Repeat NumberOfShots times
            await Task.CompletedTask;
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void StopStack()
    {
        IsRunning = false;
    }
}
