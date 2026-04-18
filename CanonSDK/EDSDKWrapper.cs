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

namespace CanonControl.CanonSDK;

public class EDSDKWrapper
{
    private IntPtr _camera;

    public bool Initialize()
    {
        return EDSDK.EdsInitializeSDK() == EdsError.EDS_ERR_OK;
    }

    public bool ConnectFirstCamera()
    {
        IntPtr cameraList;

        if (EDSDK.EdsGetCameraList(out cameraList) != EdsError.EDS_ERR_OK)
            return false;

        int count;
        if (EDSDK.EdsGetChildCount(cameraList, out count) != EdsError.EDS_ERR_OK || count == 0)
            return false;

        if (EDSDK.EdsGetChildAtIndex(cameraList, 0, out _camera) != EdsError.EDS_ERR_OK)
            return false;

        if (EDSDK.EdsOpenSession(_camera) != EdsError.EDS_ERR_OK)
            return false;

        return true;
    }

    public string GetCameraName()
    {
        if (_camera == IntPtr.Zero)
            return "No camera";

        if (EDSDK.EdsGetDeviceInfo(_camera, out var info) == EdsError.EDS_ERR_OK)
            return info.szDeviceDescription;

        return "Unknown";
    }

    public void Close()
    {
        if (_camera != IntPtr.Zero)
        {
            EDSDK.EdsCloseSession(_camera);
            EDSDK.EdsRelease(_camera);
        }

        EDSDK.EdsTerminateSDK();
    }
}
