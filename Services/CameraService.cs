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
using System.Threading.Tasks;
using CanonControl.CanonSDK;

namespace CanonControl.Services;

public class CameraService
{
    private readonly EDSDKWrapper _sdk = new();

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

    public void StartLiveView()
    {
        // TODO: implement live view functionality using the SDK
    }
}
