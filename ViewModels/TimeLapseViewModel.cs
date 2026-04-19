using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class TimeLapseViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private int _intervalSeconds = 5;

    [ObservableProperty]
    private int _numberOfShots = 100;

    [ObservableProperty]
    private bool _isRunning;

    public TimeLapseViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task StartLapse()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            for (int i = 0; i < NumberOfShots && !token.IsCancellationRequested; i++)
            {
                // TODO: Implement actual picture capture via CameraService
                // await _cameraService.TakePictureAsync();

                if (i < NumberOfShots - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected when StopLapse is called
        }
        finally
        {
            IsRunning = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public void StopLapse()
    {
        _cancellationTokenSource?.Cancel();
    }
}
