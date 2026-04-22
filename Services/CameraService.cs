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
using CanonControl.CanonSDK;
using CanonControl.Models;

namespace CanonControl.Services;

public class CameraService
{
    private readonly EDSDKWrapper _sdk = new();
    private readonly object _cameraLock = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _focusCts;
    private Task? _liveViewTask;
    private volatile bool _isEvfDownloadPaused = false;

    #region Settings
    public bool LiveViewDuringAutoFocus { get; set; } = true;
    public string SavePath
    {
        get => _sdk.SavePath;
        set => _sdk.SavePath = value;
    }

    public SaveDestination SaveDestination
    {
        get => _sdk.SaveDestination;
        set
        {
            lock (_cameraLock)
            {
                _sdk.SaveDestination = value;
                _sdk.ApplySaveDestination(); // update camera immediately if already connected
            }
        }
    }

    #endregion Settings

    #region Connect and Startup

    public async Task<bool> ConnectAsync(int timeoutSeconds = 10)
    {
        NativeLibraryLoader.LoadEDSDK();

        if (!_sdk.Initialize())
            return false;

        // poll for camera connection with configurable timeout
        const int delayMs = 500;
        int maxAttempts = (timeoutSeconds * 1000) / delayMs; // convert seconds to attempts

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_sdk.ConnectFirstCamera())
            {
                return true;
            }

            // wait before next attempt (non-blocking)
            await Task.Delay(delayMs);
        }
        return false;
    }

    public bool Connect()
    {
        return ConnectAsync().GetAwaiter().GetResult();
    }

    public void Disconnect()
    {
        // stop live view and wait for task to complete
        StopLiveView();

        // wait for live view task to fully complete before closing SDK
        // this prevents deadlock from closing while task holds _cameraLock
        if (_liveViewTask != null && !_liveViewTask.IsCompleted)
        {
            try
            {
                // wait up to 1 second for task to complete
                _liveViewTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                // task was cancelled, which is expected
            }
            _liveViewTask = null;
        }
        _sdk.Close();
    }

    public string GetCameraName()
    {
        return _sdk.GetCameraName();
    }

    #endregion Connect and Startup

    #region Live View

    public async Task StartLiveViewAsync(Action<byte[]> onFrame, int frameRate = 30)
    {
        await StartEvfAsync(onFrame, frameRate);
    }

    public async Task StartEvfAsync(Action<byte[]> onFrame, int frameRate = 30)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _sdk.StartEvf();

        // calculate delay between frames based on frame rate
        // frameRate (fps) -> delay (ms) = 1000 / fps
        int delayMs = 1000 / frameRate;

        try
        {
            _liveViewTask = Task.Run(
                async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        // pause live view when adjusting focus, to prevent camera from getting overwhelmed
                        if (_isEvfDownloadPaused)
                        {
                            await Task.Delay(50, token);
                            continue;
                        }

                        byte[]? frame;

                        lock (_cameraLock)
                        {
                            frame = _sdk.DownloadEvfFrame();
                        }

                        if (frame != null)
                            onFrame(frame);

                        await Task.Delay(delayMs, token);
                    }
                },
                token
            );
            await _liveViewTask;
        }
        catch (OperationCanceledException)
        {
            // Cancelamento esperado
        }
    }

    public void StopLiveView()
    {
        EndEvf();
    }

    public void EndEvf()
    {
        // cancel the live view task first
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        // stop any ongoing focus operations
        StopFocus();
        SetEvfAutoFocus(false);

        // wait a bit for the live view task to exit its loop and release the lock
        Thread.Sleep(100);

        // now safe to end EVF
        lock (_cameraLock)
        {
            _sdk.EndEvf();
        }

        // clean up cancellation token source
        if (_cts != null)
        {
            _cts.Dispose();
            _cts = null;
        }
    }
    #endregion Live View

    #region Focus Control

    public void FocusNearFine()
    {
        _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
    }

    public void FocusNearMedium()
    {
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            _sdk.DriveLensNear(EdsEvfDriveLens.Near2);
            Thread.Sleep(100);
            _isEvfDownloadPaused = false;
        }
    }

    public void StartFocusNear()
    {
        StopFocus(); // make sure to stop any existing focus operation before starting a new one

        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    lock (_cameraLock)
                    {
                        _sdk.DriveLensNear(EdsEvfDriveLens.Near2);
                    }

                    await Task.Delay(100, token); // focus adjustment interval
                }
            },
            token
        );
    }

    public void FocusNearCoarse()
    {
        _sdk.DriveLensNear(EdsEvfDriveLens.Near3);
    }

    public void FocusFarFine()
    {
        _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
    }

    public void FocusFarMedium()
    {
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            _sdk.DriveLensFar(EdsEvfDriveLens.Far2);
            Thread.Sleep(100);
            _isEvfDownloadPaused = false;
        }
    }

    public void StartFocusFar()
    {
        StopFocus();

        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    lock (_cameraLock)
                    {
                        _sdk.DriveLensFar(EdsEvfDriveLens.Far2);
                    }

                    await Task.Delay(100, token);
                }
            },
            token
        );
    }

    public void FocusFarCoarse()
    {
        _sdk.DriveLensFar(EdsEvfDriveLens.Far3);
    }

    public void StartAutoFocus()
    {
        SetEvfAutoFocus(true);
    }

    public void StopAutoFocus()
    {
        SetEvfAutoFocus(false);
    }

    public void SetEvfAutoFocus(bool enabled)
    {
        lock (_cameraLock)
        {
            // pause live view during autofocus if setting is disabled
            if (enabled && !LiveViewDuringAutoFocus)
            {
                _isEvfDownloadPaused = true;
            }

            _sdk.SetEvfAutoFocus(enabled);

            if (!enabled)
            {
                // small delay after stopping autofocus to ensure camera is ready
                Thread.Sleep(200);

                // resume live view if it was paused
                if (!LiveViewDuringAutoFocus)
                {
                    _isEvfDownloadPaused = false;
                }
            }
        }
    }

    public void StopFocus()
    {
        _focusCts?.Cancel();
        _focusCts = null;
    }

    public void TakePicture()
    {
        // temporarily pause live view downloads to reduce lock contention
        _isEvfDownloadPaused = true;

        try
        {
            Console.WriteLine("[TakePicture] Acquiring lock...");
            lock (_cameraLock)
            {
                Console.WriteLine("[TakePicture] Lock acquired, calling SDK...");
                _sdk.TakePicture();
                Console.WriteLine("[TakePicture] SDK call completed");
            }
            Console.WriteLine("[TakePicture] Lock released");

            // wait for camera to process the shot and trigger download
            // don't hold the lock during this wait
            Thread.Sleep(200);
            Console.WriteLine("[TakePicture] Wait completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TakePicture] Error: {ex.Message}");
            throw;
        }
        finally
        {
            // resume live view downloads
            _isEvfDownloadPaused = false;
            Console.WriteLine("[TakePicture] Live view resumed");
        }
    }

    #endregion Focus Control

    #region Camera Settings

    public string GetShutterSpeed()
    {
        lock (_cameraLock)
        {
            return _sdk.GetShutterSpeed();
        }
    }

    public string GetAperture()
    {
        lock (_cameraLock)
        {
            return _sdk.GetAperture();
        }
    }

    public string GetIso()
    {
        lock (_cameraLock)
        {
            return _sdk.GetIso();
        }
    }

    public bool IsAutoIso()
    {
        lock (_cameraLock)
        {
            return _sdk.IsAutoIso();
        }
    }

    #endregion Camera Settings

    #region Property Management

    public bool SetShutterSpeed(uint tvValue)
    {
        lock (_cameraLock)
        {
            return _sdk.SetProperty(EdsPropertyID.PropID_Tv, tvValue);
        }
    }

    public bool SetAperture(uint avValue)
    {
        lock (_cameraLock)
        {
            return _sdk.SetProperty(EdsPropertyID.PropID_Av, avValue);
        }
    }

    public bool SetIso(uint isoValue)
    {
        lock (_cameraLock)
        {
            return _sdk.SetProperty(EdsPropertyID.PropID_ISOSpeed, isoValue);
        }
    }

    public bool TryGetPropertyValue(uint propertyId, out uint value)
    {
        lock (_cameraLock)
        {
            return _sdk.TryGetPropertyValue(propertyId, out value);
        }
    }

    public string FormatPropertyValue(uint propertyId, uint value)
    {
        lock (_cameraLock)
        {
            return _sdk.FormatPropertyValue(propertyId, value);
        }
    }

    public bool TrySetPropertyValue(uint propertyId, uint value)
    {
        lock (_cameraLock)
        {
            return _sdk.SetProperty(propertyId, value);
        }
    }

    public bool TrySetPropertyRelativeToBase(
        uint propertyId,
        uint baseValue,
        double stopOffset,
        out uint appliedValue
    )
    {
        lock (_cameraLock)
        {
            appliedValue = baseValue;

            if (
                !_sdk.TryGetShiftedPropertyValue(
                    propertyId,
                    baseValue,
                    stopOffset,
                    out var targetValue
                )
            )
            {
                return false;
            }

            if (!_sdk.SetProperty(propertyId, targetValue))
            {
                return false;
            }

            appliedValue = targetValue;
            return true;
        }
    }

    public bool IncrementShutterSpeed()
    {
        lock (_cameraLock)
        {
            if (_sdk.GetNextPropertyValue(EdsPropertyID.PropID_Tv, out var nextValue))
            {
                return _sdk.SetProperty(EdsPropertyID.PropID_Tv, nextValue);
            }
            return false;
        }
    }

    public bool DecrementShutterSpeed()
    {
        lock (_cameraLock)
        {
            if (_sdk.GetPreviousPropertyValue(EdsPropertyID.PropID_Tv, out var prevValue))
            {
                return _sdk.SetProperty(EdsPropertyID.PropID_Tv, prevValue);
            }
            return false;
        }
    }

    public bool IncrementAperture()
    {
        lock (_cameraLock)
        {
            if (_sdk.GetNextPropertyValue(EdsPropertyID.PropID_Av, out var nextValue))
            {
                return _sdk.SetProperty(EdsPropertyID.PropID_Av, nextValue);
            }
            return false;
        }
    }

    public bool DecrementAperture()
    {
        lock (_cameraLock)
        {
            if (_sdk.GetPreviousPropertyValue(EdsPropertyID.PropID_Av, out var prevValue))
            {
                return _sdk.SetProperty(EdsPropertyID.PropID_Av, prevValue);
            }
            return false;
        }
    }

    public bool IncrementIso()
    {
        lock (_cameraLock)
        {
            if (_sdk.GetNextPropertyValue(EdsPropertyID.PropID_ISOSpeed, out var nextValue))
            {
                return _sdk.SetProperty(EdsPropertyID.PropID_ISOSpeed, nextValue);
            }
            return false;
        }
    }

    public bool DecrementIso()
    {
        lock (_cameraLock)
        {
            if (_sdk.GetPreviousPropertyValue(EdsPropertyID.PropID_ISOSpeed, out var prevValue))
            {
                return _sdk.SetProperty(EdsPropertyID.PropID_ISOSpeed, prevValue);
            }
            return false;
        }
    }

    #endregion Property Management

    #region Histogram

    public HistogramData? GetHistogramData()
    {
        lock (_cameraLock)
        {
            return _sdk.GetHistogramData();
        }
    }

    #endregion Histogram
}
