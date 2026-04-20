using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class ExposureBracketingViewModel : ViewModelBase
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
            6 => 3.0, // 3 stops
            _ => 1.0,
        };
    }

    // start the exposure bracketing sequence
    public async Task StartBracketing()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        CurrentShot = 0;
        Status = "Running...";

        try
        {
            // store base exposure value at start
            string baseExposure =
                ExposureParameterIndex == 0
                    ? _cameraService.GetShutterSpeed()
                    : _cameraService.GetIso();

            Console.WriteLine($"[Bracketing] Base exposure: {baseExposure}");

            // match camera's built-in behavior:
            // shot 1: 0 (base exposure - current camera setting, e.g., 1/60)
            // shot 2: -stepSize (underexposed, e.g., 1/250 for -2 stops)
            // shot 3: +stepSize (overexposed, e.g., 1/15 for +2 stops)

            // define the sequence: 0, -1, +1 for 3 shots
            double[] exposureOffsets = NumberOfShots switch
            {
                3 => new[] { 0.0, -StepSize, StepSize },
                5 => new[] { 0.0, -StepSize, StepSize, -2 * StepSize, 2 * StepSize },
                7 => new[]
                {
                    0.0,
                    -StepSize,
                    StepSize,
                    -2 * StepSize,
                    2 * StepSize,
                    -3 * StepSize,
                    3 * StepSize,
                },
                _ => new[] { 0.0 }, // fallback for other shot counts
            };

            for (int i = 0; i < NumberOfShots && IsRunning; i++)
            {
                CurrentShot = i + 1;
                Status = $"Shot {i + 1} of {NumberOfShots}";

                // get the exposure offset for this shot
                double exposureOffset = i < exposureOffsets.Length ? exposureOffsets[i] : 0.0;

                // calculate number of steps to adjust (each increment/decrement is typically 1/3 stop)
                int steps = (int)System.Math.Round(exposureOffset * 3);

                Console.WriteLine(
                    $"[Bracketing] Shot {i + 1}: offset={exposureOffset} stops, steps={steps}"
                );

                // apply exposure adjustment relative to base exposure
                if (ExposureParameterIndex == 0) // shutter Speed
                {
                    if (steps > 0)
                    {
                        // slower shutter speed (brighter/overexposed)
                        for (int s = 0; s < steps; s++)
                        {
                            _cameraService.DecrementShutterSpeed();
                            await Task.Delay(150); // increased delay for camera to process
                        }
                    }
                    else if (steps < 0)
                    {
                        // faster shutter speed (darker/underexposed)
                        for (int s = 0; s < -steps; s++)
                        {
                            _cameraService.IncrementShutterSpeed();
                            await Task.Delay(150); // increased delay
                        }
                    }
                }
                else // ISO
                {
                    if (steps > 0)
                    {
                        // higher ISO (brighter/overexposed)
                        for (int s = 0; s < steps; s++)
                        {
                            _cameraService.IncrementIso();
                            await Task.Delay(150); // increased delay
                        }
                    }
                    else if (steps < 0)
                    {
                        // lower ISO (darker/underexposed)
                        for (int s = 0; s < -steps; s++)
                        {
                            _cameraService.DecrementIso();
                            await Task.Delay(150); // increased delay
                        }
                    }
                }

                // wait for camera to apply settings
                await Task.Delay(500);

                // verify and log current exposure before taking picture
                string currentExposure =
                    ExposureParameterIndex == 0
                        ? _cameraService.GetShutterSpeed()
                        : _cameraService.GetIso();
                Console.WriteLine(
                    $"[Bracketing] Shot {i + 1}: taking picture at {currentExposure}"
                );

                // take picture
                await Task.Run(() => _cameraService.TakePicture());

                // wait for image capture to complete
                await Task.Delay(500);

                // after taking the shot, restore to base exposure for the next shot
                // (reverse the adjustment we just made)
                if (i < NumberOfShots - 1) // don't restore after the last shot
                {
                    Console.WriteLine($"[Bracketing] Restoring to base exposure: {baseExposure}");

                    if (ExposureParameterIndex == 0) // shutter Speed
                    {
                        if (steps > 0)
                        {
                            // restore: faster shutter speed (reverse the decrement)
                            for (int s = 0; s < steps; s++)
                            {
                                _cameraService.IncrementShutterSpeed();
                                await Task.Delay(150); // increased delay
                            }
                        }
                        else if (steps < 0)
                        {
                            // restore: slower shutter speed (reverse the increment)
                            for (int s = 0; s < -steps; s++)
                            {
                                _cameraService.DecrementShutterSpeed();
                                await Task.Delay(150); // increased delay
                            }
                        }
                    }
                    else // ISO
                    {
                        if (steps > 0)
                        {
                            // restore: lower ISO (reverse the increment)
                            for (int s = 0; s < steps; s++)
                            {
                                _cameraService.DecrementIso();
                                await Task.Delay(150); // increased delay
                            }
                        }
                        else if (steps < 0)
                        {
                            // restore: higher ISO (reverse the decrement)
                            for (int s = 0; s < -steps; s++)
                            {
                                _cameraService.IncrementIso();
                                await Task.Delay(150); // increased delay
                            }
                        }
                    }

                    // extra wait after restoration to ensure camera is ready
                    await Task.Delay(500);

                    // verify restoration was successful
                    string restoredExposure =
                        ExposureParameterIndex == 0
                            ? _cameraService.GetShutterSpeed()
                            : _cameraService.GetIso();
                    Console.WriteLine(
                        $"[Bracketing] Restored to: {restoredExposure} (expected: {baseExposure})"
                    );
                }
            }

            // final restoration to base exposure (after last shot)
            double lastOffset = exposureOffsets[NumberOfShots - 1];
            int lastSteps = (int)System.Math.Round(lastOffset * 3);

            Console.WriteLine($"[Bracketing] Final restoration to base: {baseExposure}");

            if (ExposureParameterIndex == 0) // shutter Speed
            {
                if (lastSteps > 0)
                {
                    for (int s = 0; s < lastSteps; s++)
                    {
                        _cameraService.IncrementShutterSpeed();
                        await Task.Delay(100);
                    }
                }
                else if (lastSteps < 0)
                {
                    for (int s = 0; s < -lastSteps; s++)
                    {
                        _cameraService.DecrementShutterSpeed();
                        await Task.Delay(100);
                    }
                }
            }
            else // ISO
            {
                if (lastSteps > 0)
                {
                    for (int s = 0; s < lastSteps; s++)
                    {
                        _cameraService.DecrementIso();
                        await Task.Delay(100);
                    }
                }
                else if (lastSteps < 0)
                {
                    for (int s = 0; s < -lastSteps; s++)
                    {
                        _cameraService.IncrementIso();
                        await Task.Delay(100);
                    }
                }
            }

            Status = IsRunning ? "Completed" : "Stopped";
        }
        catch (System.Exception ex)
        {
            Status = $"Error: {ex.Message}";
            Console.WriteLine($"[Bracketing] Error: {ex}");
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
