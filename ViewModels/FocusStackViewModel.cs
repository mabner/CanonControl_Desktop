/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class FocusStackViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationTokenSource? _pollCts; // cancellation token for the step-position polling timer.

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
    [NotifyPropertyChangedFor(nameof(ExposureSummary))]
    private string _shutterSpeed = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExposureSummary))]
    private string _aperture = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExposureSummary))]
    private string _iso = string.Empty;

    [ObservableProperty]
    private double _shootIntervalSeconds = 2.0;

    // --- A/B point registration ---

    // raw step counter value recorded when the user pressed Set A (always 0 after reset).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPointASet))]
    [NotifyPropertyChangedFor(nameof(IsRangeValid))]
    [NotifyPropertyChangedFor(nameof(PointALabel))]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    [NotifyCanExecuteChangedFor(nameof(RegisterPointBCommand))]
    private int? _focusPointA;

    // raw step counter value recorded when the user pressed Set B.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPointBSet))]
    [NotifyPropertyChangedFor(nameof(IsRangeValid))]
    [NotifyPropertyChangedFor(nameof(PointBLabel))]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int? _focusPointB;

    // live display of the current focus step offset from point A (shown while user drives focus to B).
    [ObservableProperty]
    private string _liveStepLabel = "Current: --";

    public FocusStackViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
        UpdateCameraSettings();
        StartPositionPolling(); // begin polling the step counter so B label stays current.
    }

    public string ExposureSummary =>
        string.IsNullOrWhiteSpace(ShutterSpeed) ? "--" : $"{ShutterSpeed} - {Aperture} - {Iso}";

    // --- Computed properties for A/B state ---

    // true once the user has pressed Set A.
    public bool IsPointASet => FocusPointA.HasValue;

    // true once the user has pressed Set B.
    public bool IsPointBSet => FocusPointB.HasValue;

    // true when both points are set and B is strictly further than A.
    public bool IsRangeValid => FocusPointA.HasValue && FocusPointB.HasValue && FocusPointB.Value > FocusPointA.Value;

    // human-readable label for point A shown in the UI.
    public string PointALabel => FocusPointA.HasValue ? "A: 0 (near)" : "A: --";

    // human-readable label for point B shown in the UI.
    public string PointBLabel => FocusPointB.HasValue ? $"B: +{FocusPointB.Value} steps" : "B: --";

    // human-readable summary of the total range between A and B.
    public string RangeLabel => IsRangeValid
        ? $"Range: {FocusPointB!.Value - FocusPointA!.Value} steps"
        : "Range: -- steps";

    // --- Commands ---

    // registers the current focus position as point A; resets the step counter to zero.
    [RelayCommand]
    private void RegisterPointA()
    {
        _cameraService.ResetFocusStepPosition();
        FocusPointA = 0;
        FocusPointB = null; // clear B so the user must re-register after moving to the new A.
        RecalculateShots();
        Status = "Point A registered. Drive focus to far end and press Set B.";
    }

    // registers the current step counter value as point B; validates that B > A.
    [RelayCommand(CanExecute = nameof(IsPointASet))]
    private void RegisterPointB()
    {
        int current = _cameraService.FocusStepPosition;
        if (current <= (FocusPointA ?? 0))
        {
            Status = "Point B must be further than point A. Drive focus farther and try again.";
            return;
        }
        FocusPointB = current;
        RecalculateShots();
        Status = $"Points set: range = {RangeLabel}, {NumberOfShots} shots calculated.";
    }

    // clears both A and B points and reverts to manual shot count.
    [RelayCommand]
    private void ClearPoints()
    {
        FocusPointA = null;
        FocusPointB = null;
        LiveStepLabel = "Current: --";
        Status = "Range cleared. Set A and B to use automatic shot calculation.";
    }

    // called automatically when StepSize changes so the shot count stays in sync.
    partial void OnStepSizeChanged(int value) => RecalculateShots();

    // --- Shot count calculation ---

    // calculates NumberOfShots from the A/B range and current StepSize.
    private void RecalculateShots()
    {
        if (!IsRangeValid || StepSize <= 0)
            return;

        int range = FocusPointB!.Value - FocusPointA!.Value;
        NumberOfShots = Math.Max(2, (int)Math.Ceiling((double)range / StepSize) + 1);
    }

    // --- Position polling (sub-task 7.3.5) ---

    // starts a 500 ms polling loop that refreshes the live step display while the ViewModel is alive.
    private void StartPositionPolling()
    {
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                // refresh the live step label so the user can watch the counter change as they drive focus.
                int pos = _cameraService.FocusStepPosition;
                LiveStepLabel = IsPointASet ? $"Current: {pos:+0;-0;0} steps from A" : "Current: --";

                // also refresh B label in case it was set externally (though commands handle this).
                OnPropertyChanged(nameof(PointBLabel));
                OnPropertyChanged(nameof(RangeLabel));

                await Task.Delay(500, token);
            }
        }, token);
    }

    // stops the polling loop; call this when the ViewModel is no longer needed.
    public void StopPositionPolling()
    {
        _pollCts?.Cancel();
        _pollCts = null;
    }

    // --- Stack execution ---

    public async Task StartStack()
    {
        if (IsRunning)
            return;

        // validate save path before attempting capture
        if (!ValidateSavePath())
            return;

        IsRunning = true;
        CurrentShot = 0;
        Status = "Running...";
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            // small delay to ensure UI is fully updated before starting
            await Task.Delay(100, token);

            // Travel to point A before starting
            if (IsPointASet && _cameraService.FocusStepPosition != FocusPointA!.Value)
            {
                Status = "Travelling to Point A...";
                while (_cameraService.FocusStepPosition != FocusPointA.Value && !token.IsCancellationRequested)
                {
                    if (_cameraService.FocusStepPosition > FocusPointA.Value)
                    {
                        _cameraService.FocusNearFine();
                    }
                    else
                    {
                        _cameraService.FocusFarFine();
                    }
                    await Task.Delay(50, token); // Allow EVF update and prevent blocking
                }
            }

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
            Console.WriteLine($"Focus stack error: {ex}");
        }
        finally
        {
            IsRunning = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private bool ValidateSavePath()
    {
        // camera-only mode: images stay on the card, no PC path needed
        if (_cameraService.SaveDestination == CanonControl.Models.SaveDestination.Camera)
            return true;

        var savePath = _cameraService.SavePath;

        if (string.IsNullOrWhiteSpace(savePath))
        {
            Status = "Error: Save path is not set. Please configure in Settings.";
            Console.WriteLine("[FocusStack] Error: SavePath is empty");
            return false;
        }

        if (!System.IO.Directory.Exists(savePath))
        {
            try
            {
                System.IO.Directory.CreateDirectory(savePath);
                Console.WriteLine($"[FocusStack] Created save directory: {savePath}");
            }
            catch (Exception ex)
            {
                Status = $"Error: Cannot create save path: {ex.Message}";
                Console.WriteLine($"[FocusStack] Error creating directory: {ex.Message}");
                return false;
            }
        }

        Console.WriteLine($"[FocusStack] Save path validated: {savePath}");
        return true;
    }

    public void StopStack()
    {
        _cancellationTokenSource?.Cancel();
    }

    public void UpdateCameraSettings()
    {
        ShutterSpeed = _cameraService.GetShutterSpeed();
        Aperture = _cameraService.GetAperture();
        Iso = _cameraService.GetIso();
    }
}
