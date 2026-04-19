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

namespace CanonControl.Services;

public class CameraService
{
    private readonly EDSDKWrapper _sdk = new();
    private readonly object _cameraLock = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _focusCts;
    private volatile bool _isEvfDownloadPaused = false;
    public bool LiveViewDuringAutoFocus { get; set; } = true;

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
        StopLiveView();

        if (_sdk != null)
        {
            _sdk.Close();
        }
    }

    public string GetCameraName()
    {
        return _sdk.GetCameraName();
    }

    #endregion Connect and Startup

    #region Live View

    public async Task StartLiveViewAsync(Action<byte[]> onFrame)
    {
        await StartEvfAsync(onFrame);
    }

    public async Task StartEvfAsync(Action<byte[]> onFrame)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _sdk.StartEvf();

        try
        {
            await Task.Run(
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

                        await Task.Delay(30, token);
                    }
                },
                token
            );
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
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        StopFocus();
        SetEvfAutoFocus(false);

        lock (_cameraLock)
        {
            _sdk.EndEvf();
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
        lock (_cameraLock)
        {
            _sdk.TakePicture();
        }
    }

    #endregion Focus Control
}
