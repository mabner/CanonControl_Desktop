using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanonControl.Models;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class RemoteCaptureViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;
    private CancellationTokenSource? _pollingCts;
    private bool _isPolling;

    [ObservableProperty]
    private string _shutterSpeed = string.Empty;

    [ObservableProperty]
    private string _aperture = string.Empty;

    [ObservableProperty]
    private string _iso = string.Empty;

    [ObservableProperty]
    private string _actualIso = string.Empty;

    [ObservableProperty]
    private bool _isAutoIso;

    [ObservableProperty]
    private int _delaySeconds = 0;

    [ObservableProperty]
    private int _countdownSeconds = 0;

    [ObservableProperty]
    private HistogramDisplayMode _histogramMode = HistogramDisplayMode.None;

    public RemoteCaptureViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task TakeSinglePicture()
    {
        if (DelaySeconds > 0)
        {
            // display countdown in UI
            for (int i = DelaySeconds; i > 0; i--)
            {
                CountdownSeconds = i;
                await Task.Delay(1000);
            }
            CountdownSeconds = 0;
        }

        _cameraService.TakePicture();
    }

    [RelayCommand]
    private void IncrementShutterSpeed()
    {
        _cameraService.IncrementShutterSpeed();
        UpdateCameraSettings();
    }

    [RelayCommand]
    private void DecrementShutterSpeed()
    {
        _cameraService.DecrementShutterSpeed();
        UpdateCameraSettings();
    }

    [RelayCommand]
    private void IncrementAperture()
    {
        _cameraService.IncrementAperture();
        UpdateCameraSettings();
    }

    [RelayCommand]
    private void DecrementAperture()
    {
        _cameraService.DecrementAperture();
        UpdateCameraSettings();
    }

    [RelayCommand]
    private void IncrementIso()
    {
        _cameraService.IncrementIso();
        UpdateCameraSettings();
    }

    [RelayCommand]
    private void DecrementIso()
    {
        _cameraService.DecrementIso();
        UpdateCameraSettings();
    }

    public void StartPolling()
    {
        if (_isPolling)
            return;

        _isPolling = true;
        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        UpdateCameraSettings();
                        await Task.Delay(500, token); // poll every 500ms
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // silently ignore errors during polling
                    }
                }
            },
            token
        );
    }

    public void StopPolling()
    {
        if (!_isPolling)
            return;

        _isPolling = false;
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    private void UpdateCameraSettings()
    {
        ShutterSpeed = _cameraService.GetShutterSpeed();
        Aperture = _cameraService.GetAperture();
        Iso = _cameraService.GetIso();
        IsAutoIso = _cameraService.IsAutoIso();

        ActualIso = string.Empty;
    }

    public void Dispose()
    {
        StopPolling();
    }
}
