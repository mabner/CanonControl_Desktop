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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CanonControl.CanonSDK;

public class EDSDKWrapper
{
    private IntPtr _camera;

    #region Initialization and Camera Management

    public bool Initialize()
    {
        return EDSDK.EdsInitializeSDK() == EdsError.EDS_ERR_OK;
    }

    public bool ConnectFirstCamera()
    {
        // close any existing connection first
        if (_camera != IntPtr.Zero)
        {
            EDSDK.EdsCloseSession(_camera);
            EDSDK.EdsRelease(_camera);
            _camera = IntPtr.Zero;
        }

        IntPtr cameraList;

        // get fresh camera list (detects newly connected cameras)
        if (EDSDK.EdsGetCameraList(out cameraList) != EdsError.EDS_ERR_OK)
            return false;

        try
        {
            int count;
            if (EDSDK.EdsGetChildCount(cameraList, out count) != EdsError.EDS_ERR_OK || count == 0)
                return false;

            if (EDSDK.EdsGetChildAtIndex(cameraList, 0, out _camera) != EdsError.EDS_ERR_OK)
                return false;

            if (EDSDK.EdsOpenSession(_camera) != EdsError.EDS_ERR_OK)
                return false;

            return true;
        }
        finally
        {
            EDSDK.EdsRelease(cameraList);
        }
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
            _camera = IntPtr.Zero;
        }

        EDSDK.EdsTerminateSDK();
    }

    #endregion Initialization and Camera Management

    #region Live View

    public bool StartEvf()
    {
        if (!TryGetUInt32Property(EdsPropertyID.Evf_Mode, out var evfMode))
        {
            return false;
        }

        if (evfMode == 0)
        {
            evfMode = 1;
            if (!TrySetUInt32Property(EdsPropertyID.Evf_Mode, evfMode))
            {
                return false;
            }
        }

        if (!TryGetUInt32Property(EdsPropertyID.Evf_OutputDevice, out var device))
        {
            return false;
        }

        device |= EdsOutputDevice.PC;

        return TrySetUInt32Property(EdsPropertyID.Evf_OutputDevice, device);
    }

    public bool EndEvf()
    {
        if (!TryGetUInt32Property(EdsPropertyID.Evf_OutputDevice, out var device))
        {
            return false;
        }

        if ((device & EdsOutputDevice.PC) == 0)
        {
            return true;
        }

        device &= ~EdsOutputDevice.PC;
        return TrySetUInt32Property(EdsPropertyID.Evf_OutputDevice, device);
    }

    public byte[]? DownloadEvfFrame()
    {
        IntPtr stream;
        IntPtr evfImage;

        EDSDK.EdsCreateMemoryStream(0, out stream);

        EDSDK.EdsCreateEvfImageRef(stream, out evfImage);

        var err = EDSDK.EdsDownloadEvfImage(_camera, evfImage);

        if (err != EdsError.EDS_ERR_OK)
        {
            EDSDK.EdsRelease(evfImage);
            EDSDK.EdsRelease(stream);

            // intelligent retry for device busy error
            if ((uint)err == 0x00000081) // DEVICE_BUSY
            {
                Thread.Sleep(50);
                return null;
            }

            return null;
        }

        EDSDK.EdsGetPointer(stream, out var pointer);

        EDSDK.EdsGetLength(stream, out var length);

        byte[] data = new byte[length];
        Marshal.Copy(pointer, data, 0, (int)length);

        EDSDK.EdsRelease(evfImage);
        EDSDK.EdsRelease(stream);

        return data; // return the JPEG data of the live view frame
    }

    #endregion Live Liew

    #region Focus Control

    public void DriveLensNear(int step)
    {
        SendCommand(EdsCameraCommand.DriveLensEvf, step);
    }

    public void DriveLensFar(int step)
    {
        SendCommand(EdsCameraCommand.DriveLensEvf, step);
    }

    public void SetEvfAutoFocus(bool enabled)
    {
        SendCommand(
            EdsCameraCommand.DoEvfAf,
            enabled ? EdsEvfAf.CameraCommand_EvfAf_ON : EdsEvfAf.CameraCommand_EvfAf_OFF
        );
    }

    public void PressShutterButton(int shutterState)
    {
        SendCommand(EdsCameraCommand.PressShutterButton, shutterState);
    }

    public void TakePicture()
    {
        // Canon sample behavior: press completely, then release.
        PressShutterButton(EdsShutterButton.Completely);
        PressShutterButton(EdsShutterButton.Off);
    }

    #endregion Focus Control

    #region Backward-compatible aliases

    public bool StartLiveView() => StartEvf();

    public byte[]? GetLiveViewFrame() => DownloadEvfFrame();

    public void FocusNear(int step) => DriveLensNear(step);

    public void FocusFar(int step) => DriveLensFar(step);

    public void StartEvfAutoFocus() => SetEvfAutoFocus(true);

    public void StopEvfAutoFocus() => SetEvfAutoFocus(false);

    #endregion Backward-compatible aliases

    private bool TryGetUInt32Property(uint propertyId, out uint value)
    {
        return EDSDK.EdsGetPropertyData(_camera, propertyId, 0, sizeof(uint), out value)
            == EdsError.EDS_ERR_OK;
    }

    private bool TrySetUInt32Property(uint propertyId, uint value)
    {
        return EDSDK.EdsSetPropertyData(_camera, propertyId, 0, sizeof(uint), ref value)
            == EdsError.EDS_ERR_OK;
    }

    public EdsError SendCommand(uint command, int param)
    {
        const int maxRetries = 10;

        for (int i = 0; i < maxRetries; i++)
        {
            var err = EDSDK.EdsSendCommand(_camera, command, param);

            if (err == EdsError.EDS_ERR_OK)
                return err;

            if (err == EdsError.EDS_ERR_DEVICE_BUSY)
            {
                Thread.Sleep(100);
                continue;
            }

            return err;
        }

        return EdsError.EDS_ERR_DEVICE_BUSY;
    }
}
