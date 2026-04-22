using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanonControl.CanonSDK;
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
    private int _exposureParameterIndex; // default to "Shutter Speed"

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

        // validate save path before attempting capture
        if (!ValidateSavePath())
            return;

        var propertyId =
            ExposureParameterIndex == 0 ? EdsPropertyID.PropID_Tv : EdsPropertyID.PropID_ISOSpeed;
        uint? baseExposureValue = null;

        IsRunning = true;
        CurrentShot = 0;
        Status = "Running...";

        try
        {
            if (!_cameraService.TryGetPropertyValue(propertyId, out var capturedBaseExposureValue))
            {
                throw new InvalidOperationException(
                    "Unable to read the current exposure from the camera."
                );
            }

            baseExposureValue = capturedBaseExposureValue;

            var baseExposure = _cameraService.FormatPropertyValue(
                propertyId,
                capturedBaseExposureValue
            );

            Console.WriteLine($"[Bracketing] Base exposure: {baseExposure}");

            var exposureOffsets = BuildExposureOffsets();

            for (int i = 0; i < NumberOfShots && IsRunning; i++)
            {
                CurrentShot = i + 1;
                Status = $"Shot {i + 1} of {NumberOfShots}";

                var exposureOffset = exposureOffsets[i];
                if (
                    !_cameraService.TrySetPropertyRelativeToBase(
                        propertyId,
                        capturedBaseExposureValue,
                        exposureOffset,
                        out var appliedExposureValue
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Failed to apply exposure offset of {exposureOffset} EV."
                    );
                }

                Console.WriteLine(
                    $"[Bracketing] Shot {i + 1}: offset={exposureOffset} stops, target={_cameraService.FormatPropertyValue(propertyId, appliedExposureValue)}"
                );

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
            }

            Status = IsRunning ? "Completed" : "Stopped";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            Console.WriteLine($"[Bracketing] Error: {ex}");
        }
        finally
        {
            if (baseExposureValue.HasValue)
            {
                _ = _cameraService.TrySetPropertyValue(propertyId, baseExposureValue.Value);
            }

            IsRunning = false;
        }
    }

    private bool ValidateSavePath()
    {
        // camera-only mode: images stay on the card, no PC path needed.
        if (_cameraService.SaveDestination == CanonControl.Models.SaveDestination.Camera)
        {
            return true;
        }

        var savePath = _cameraService.SavePath;

        if (string.IsNullOrWhiteSpace(savePath))
        {
            Status = "Error: Save path is not set. Please configure in Settings.";
            Console.WriteLine("[ExposureBracketing] Error: SavePath is empty");
            return false;
        }

        if (!System.IO.Directory.Exists(savePath))
        {
            try
            {
                System.IO.Directory.CreateDirectory(savePath);
                Console.WriteLine($"[ExposureBracketing] Created save directory: {savePath}");
            }
            catch (Exception ex)
            {
                Status = $"Error: Cannot create save path: {ex.Message}";
                Console.WriteLine($"[ExposureBracketing] Error creating directory: {ex.Message}");
                return false;
            }
        }

        Console.WriteLine($"[ExposureBracketing] Save path validated: {savePath}");
        return true;
    }

    private double[] BuildExposureOffsets()
    {
        if (NumberOfShots <= 1)
        {
            return new[] { 0.0 };
        }

        var offsets = new List<double> { 0.0 };
        for (var level = 1; offsets.Count < NumberOfShots; level++)
        {
            offsets.Add(-level * StepSize);
            if (offsets.Count < NumberOfShots)
            {
                offsets.Add(level * StepSize);
            }
        }

        return offsets.ToArray();
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
