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

    public bool StartLiveView()
    {
        uint device = EdsOutputDevice.PC;

        var err = EDSDK.EdsSetPropertyData(
            _camera,
            EdsPropertyID.Evf_OutputDevice,
            0,
            sizeof(uint),
            ref device
        ); // direciona o live view do lcd para o pc

        return err == EdsError.EDS_ERR_OK;
    }

    public byte[] GetLiveViewFrame()
    {
        IntPtr stream;
        IntPtr evfImage;

        EDSDK.EdsCreateMemoryStream(0, out stream);

        EDSDK.EdsCreateEvfImageRef(stream, out evfImage);

        var err = EDSDK.EdsDownloadEvfImage(_camera, evfImage);

        if (err != EdsError.EDS_ERR_OK)
            return null;

        EDSDK.EdsGetPointer(stream, out var pointer);

        EDSDK.EdsGetLength(stream, out var length);

        byte[] data = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(pointer, data, 0, (int)length);

        EDSDK.EdsRelease(evfImage);
        EDSDK.EdsRelease(stream);

        return data;
    }
}
