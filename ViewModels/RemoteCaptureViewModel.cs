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
    [NotifyPropertyChangedFor(nameof(DelaySecondsIndex))]
    private int _delaySeconds = 0;

    [ObservableProperty]
    private int _countdownSeconds = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistogramModeIndex))]
    private HistogramDisplayMode _histogramMode = HistogramDisplayMode.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompositionModeIndex), nameof(IsGoldenCompositionMode))]
    private CompositionAidMode _compositionMode = CompositionAidMode.None;

    [ObservableProperty]
    private bool _isCompositionMirrored = false;

    [ObservableProperty]
    private int _compositionRotation = 0;

    // ComboBox index properties for MVVM binding
    public int DelaySecondsIndex
    {
        get =>
            DelaySeconds switch
            {
                0 => 0,
                2 => 1,
                5 => 2,
                10 => 3,
                _ => 0,
            };
        set
        {
            DelaySeconds = value switch
            {
                0 => 0,
                1 => 2,
                2 => 5,
                3 => 10,
                _ => 0,
            };
        }
    }

    public int HistogramModeIndex
    {
        get => (int)HistogramMode;
        set => HistogramMode = (HistogramDisplayMode)value;
    }

    public int CompositionModeIndex
    {
        get => (int)CompositionMode;
        set => CompositionMode = (CompositionAidMode)value;
    }

    public bool IsGoldenCompositionMode =>
        CompositionMode == CompositionAidMode.GoldenRatio
        || CompositionMode == CompositionAidMode.GoldenTriangle;

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

    [RelayCommand]
    private void ToggleCompositionMirror()
    {
        IsCompositionMirrored = !IsCompositionMirrored;
    }

    [RelayCommand]
    private void RotateComposition()
    {
        CompositionRotation = (CompositionRotation + 90) % 360;
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
