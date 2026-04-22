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

    [ObservableProperty]
    private int _currentShot;

    [ObservableProperty]
    private string _status = "Ready";

    public TimeLapseViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
    }

    public async Task StartLapse()
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
            for (int i = 1; i <= NumberOfShots && !token.IsCancellationRequested; i++)
            {
                CurrentShot = i;
                Status = $"Shot {i} of {NumberOfShots}";

                // Take picture
                await Task.Run(() => _cameraService.TakePicture(), token);

                // Wait for interval (except after last shot)
                if (i < NumberOfShots && !token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), token);
                }
            }

            Status = token.IsCancellationRequested ? "Stopped" : "Completed";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
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
            Console.WriteLine("[TimeLapse] Error: SavePath is empty");
            return false;
        }

        if (!System.IO.Directory.Exists(savePath))
        {
            try
            {
                System.IO.Directory.CreateDirectory(savePath);
                Console.WriteLine($"[TimeLapse] Created save directory: {savePath}");
            }
            catch (Exception ex)
            {
                Status = $"Error: Cannot create save path: {ex.Message}";
                Console.WriteLine($"[TimeLapse] Error creating directory: {ex.Message}");
                return false;
            }
        }

        Console.WriteLine($"[TimeLapse] Save path validated: {savePath}");
        return true;
    }

    public void StopLapse()
    {
        _cancellationTokenSource?.Cancel();
    }
}
