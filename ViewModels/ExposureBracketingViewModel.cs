using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public class ExposureBracketingViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    [ObservableProperty]
    private int _numberOfShots = 3;

    [ObservableProperty]
    private double _stepSize = 1.0;

    [ObservableProperty]
    private int _stepSizeIndex = 3; // default to "1 stop"

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _currentShot;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private int _exposureParameterIndex = 0; // default to "Shutter Speed"

    public ExposureBracketingViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    partial void OnStepSizeIndexChanged(int value)
    {
        // map ComboBox index to actual step size values
        StepSize = value switch
        {
            0 => 1.0 / 3.0, // 1/3 stop
            1 => 0.5, // 1/2 stop
            2 => 2.0 / 3.0, // 2/3 stop
            3 => 1.0, // 1 stop
            4 => 1.5, // 1.5 stops
            5 => 2.0, // 2 stops
            _ => 1.0,
        };
    }

    // Start the exposure bracketing sequence
    public async Task StartBracketing()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        CurrentShot = 0;
        Status = "Running...";

        try
        {
            // calculate the exposure adjustments
            // for example, with 3 shots and step size 1.0:
            // shot 1: -1 stop
            // shot 2: 0 (base exposure)
            // shot 3: +1 stop

            int middleShot = (NumberOfShots + 1) / 2;

            // store original settings to restore later
            string? originalShutterSpeed = null;
            string? originalIso = null;

            if (ExposureParameterIndex == 0) // shutter Speed
            {
                originalShutterSpeed = _cameraService.GetShutterSpeed();
            }
            else // ISO
            {
                originalIso = _cameraService.GetIso();
            }

            for (int i = 1; i <= NumberOfShots && IsRunning; i++)
            {
                CurrentShot = i;
                Status = $"Shot {i} of {NumberOfShots}";

                // calculate exposure offset in stops
                double exposureOffset = (i - middleShot) * StepSize;

                // calculate number of steps to adjust (each increment/decrement is typically 1/3 stop)
                int steps = (int)System.Math.Round(exposureOffset * 3);

                // apply exposure adjustment
                if (ExposureParameterIndex == 0) // shutter Speed
                {
                    if (steps > 0)
                    {
                        // slower shutter speed (brighter)
                        for (int s = 0; s < steps; s++)
                        {
                            _cameraService.DecrementShutterSpeed();
                            await Task.Delay(50); // small delay between adjustments
                        }
                    }
                    else if (steps < 0)
                    {
                        // faster shutter speed (darker)
                        for (int s = 0; s < -steps; s++)
                        {
                            _cameraService.IncrementShutterSpeed();
                            await Task.Delay(50);
                        }
                    }
                }
                else // ISO
                {
                    if (steps > 0)
                    {
                        // higher ISO (brighter)
                        for (int s = 0; s < steps; s++)
                        {
                            _cameraService.IncrementIso();
                            await Task.Delay(50);
                        }
                    }
                    else if (steps < 0)
                    {
                        // lower ISO (darker)
                        for (int s = 0; s < -steps; s++)
                        {
                            _cameraService.DecrementIso();
                            await Task.Delay(50);
                        }
                    }
                }

                // wait for camera to apply settings
                await Task.Delay(200);

                // take picture
                await Task.Run(() => _cameraService.TakePicture());

                // wait between shots
                if (i < NumberOfShots)
                {
                    await Task.Delay(500);
                }
            }

            // restore original settings
            // note: This is a simplified restoration - ideally we'd store the exact property values
            // and restore them, but for now we'll just reset to middle exposure
            if (ExposureParameterIndex == 0 && originalShutterSpeed != null)
            {
                // try to restore by moving back to middle
                int middleSteps = (int)System.Math.Round((NumberOfShots / 2) * StepSize * 3);
                for (int s = 0; s < middleSteps; s++)
                {
                    _cameraService.IncrementShutterSpeed();
                    await Task.Delay(50);
                }
            }
            else if (ExposureParameterIndex == 1 && originalIso != null)
            {
                int middleSteps = (int)System.Math.Round((NumberOfShots / 2) * StepSize * 3);
                for (int s = 0; s < middleSteps; s++)
                {
                    _cameraService.DecrementIso();
                    await Task.Delay(50);
                }
            }

            Status = IsRunning ? "Completed" : "Stopped";
        }
        catch (System.Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    // stop the exposure bracketing sequence
    public void StopBracketing()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        Status = "Stopped";
    }
}
