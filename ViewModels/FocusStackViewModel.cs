using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class FocusStackViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private int _numberOfShots = 10;

    [ObservableProperty]
    private int _stepSize = 1;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _currentShot;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _shootIntervalSeconds = 2.0;

    public FocusStackViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task StartStack()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        CurrentShot = 0;
        Status = "Running...";
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            for (int i = 1; i <= NumberOfShots && !token.IsCancellationRequested; i++)
            {
                CurrentShot = i;
                Status = $"Shot {i} of {NumberOfShots}";

                // take picture
                await Task.Run(() => _cameraService.TakePicture(), token);

                // move focus for next shot (except after last shot)
                if (i < NumberOfShots && !token.IsCancellationRequested)
                {
                    // drive lens by step size
                    // positive step size moves focus farther
                    for (int step = 0; step < StepSize; step++)
                    {
                        _cameraService.FocusFarFine();
                        await Task.Delay(100, token); // small delay between steps
                    }

                    // wait for configured shoot interval (convert seconds to milliseconds)
                    await Task.Delay((int)(ShootIntervalSeconds * 1000), token);
                }
            }

            Status = token.IsCancellationRequested ? "Stopped" : "Completed";
        }
        catch (System.OperationCanceledException)
        {
            Status = "Stopped";
        }
        catch (System.Exception ex)
        {
            Status = $"Error: {ex.Message}";
            // log full exception for debugging
            System.Console.WriteLine($"Focus stack error: {ex}");
        }
        finally
        {
            IsRunning = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public void StopStack()
    {
        _cancellationTokenSource?.Cancel();
    }
}
