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
    private CancellationTokenSource _cts;

    public bool Connect()
    {
        NativeLibraryLoader.LoadEDSDK();

        if (!_sdk.Initialize())
            return false;

        return _sdk.ConnectFirstCamera();
    }

    public string GetCameraName()
    {
        return _sdk.GetCameraName();
    }

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
                        var frame = _sdk.GetLiveViewFrame();

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
}
