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
    private CancellationTokenSource _cts;

    #region Connect and Startup

    public bool Connect()
    {
        NativeLibraryLoader.LoadEDSDK();

        if (!_sdk.Initialize())
            return false;

        return _sdk.ConnectFirstCamera();
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
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _sdk.StartLiveView();

        try
        {
            await Task.Run(
                async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        byte[] frame;

                        lock (_cameraLock)
                        {
                            frame = _sdk.GetLiveViewFrame();
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
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
    #endregion Live View

    #region Focus Control

    public void FocusNearFine()
    {
        _sdk.FocusNear(EdsEvfDriveLens.Near1);
    }

    public void FocusNearMedium()
    {
        lock (_cameraLock)
        {
            _sdk.FocusNear(EdsEvfDriveLens.Near2);
        }
    }

    public void FocusNearCoarse()
    {
        _sdk.FocusNear(EdsEvfDriveLens.Near3);
    }

    public void FocusFarFine()
    {
        _sdk.FocusFar(EdsEvfDriveLens.Far1);
    }

    public void FocusFarMedium()
    {
        lock (_cameraLock)
        {
            _sdk.FocusFar(EdsEvfDriveLens.Far2);
        }
    }

    public void FocusFarCoarse()
    {
        _sdk.FocusFar(EdsEvfDriveLens.Far3);
    }

    public void AutoFocus()
    {
        lock (_cameraLock)
        {
            _sdk.AutoFocus();
        }
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
