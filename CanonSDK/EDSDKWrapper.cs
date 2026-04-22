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
using CanonControl.Models;

namespace CanonControl.CanonSDK;

public class EDSDKWrapper
{
    private IntPtr _camera;

    private EDSDK.EdsObjectEventHandler? _objectEventHandler;

    public string SavePath { get; set; } = string.Empty;

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

            // get camera reference
            if (EDSDK.EdsGetChildAtIndex(cameraList, 0, out _camera) != EdsError.EDS_ERR_OK)
                return false;

            // create event handler delegate BEFORE opening session
            // store as instance field to prevent garbage collection
            _objectEventHandler = new EDSDK.EdsObjectEventHandler(OnObjectEvent);

            // register event handler BEFORE opening session
            // this is the correct sequence per Canon's sample code
            var err = EDSDK.EdsSetObjectEventHandler(
                _camera,
                EdsObjectEvent.All,
                _objectEventHandler,
                IntPtr.Zero
            );
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"Failed to register event handler: {err}");
                EDSDK.EdsRelease(_camera);
                _camera = IntPtr.Zero;
                _objectEventHandler = null;
                return false;
            }

            // NOW open session (after event handler registration)
            if (EDSDK.EdsOpenSession(_camera) != EdsError.EDS_ERR_OK)
            {
                // unregister handler on failure
                EDSDK.EdsSetObjectEventHandler(_camera, EdsObjectEvent.All, null, IntPtr.Zero);
                EDSDK.EdsRelease(_camera);
                _camera = IntPtr.Zero;
                _objectEventHandler = null;
                return false;
            }

            Console.WriteLine("Camera connected successfully with event handlers registered");
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
            // unregister event handler before closing session
            if (_objectEventHandler != null)
            {
                EDSDK.EdsSetObjectEventHandler(_camera, EdsObjectEvent.All, null, IntPtr.Zero);
                _objectEventHandler = null;
            }

            EDSDK.EdsCloseSession(_camera);
            EDSDK.EdsRelease(_camera);
            _camera = IntPtr.Zero;
            _objectEventHandler = null; // clear the reference to allow GC
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

    public HistogramData? GetHistogramData()
    {
        IntPtr stream;
        IntPtr evfImage;

        // create buffer stream
        EDSDK.EdsCreateMemoryStream(0, out stream);

        // create frame reference
        EDSDK.EdsCreateEvfImageRef(stream, out evfImage);

        // download live view frame to get histogram data
        var err = EDSDK.EdsDownloadEvfImage(_camera, evfImage);

        if (err != EdsError.EDS_ERR_OK)
        {
            EDSDK.EdsRelease(evfImage);
            EDSDK.EdsRelease(stream);
            return null;
        }

        var histogram = new HistogramData();

        // get histogram status
        uint status = 0;
        if (
            EDSDK.EdsGetPropertyData(
                evfImage,
                EdsPropertyID.Evf_HistogramStatus,
                0,
                sizeof(uint),
                out status
            ) == EdsError.EDS_ERR_OK
        )
        {
            histogram.Status = (int)status;
        }

        // get histogram data if visible
        if (histogram.Status == 1) // Normal
        {
            // get Y (Luminance) histogram
            EDSDK.EdsGetPropertyData(
                evfImage,
                EdsPropertyID.Evf_HistogramY,
                0,
                256 * sizeof(uint),
                histogram.Luminance
            );

            // get RGB histograms
            EDSDK.EdsGetPropertyData(
                evfImage,
                EdsPropertyID.Evf_HistogramR,
                0,
                256 * sizeof(uint),
                histogram.Red
            );
            EDSDK.EdsGetPropertyData(
                evfImage,
                EdsPropertyID.Evf_HistogramG,
                0,
                256 * sizeof(uint),
                histogram.Green
            );
            EDSDK.EdsGetPropertyData(
                evfImage,
                EdsPropertyID.Evf_HistogramB,
                0,
                256 * sizeof(uint),
                histogram.Blue
            );
        }

        // release resources
        EDSDK.EdsRelease(evfImage);
        EDSDK.EdsRelease(stream);

        return histogram;
    }

    #endregion Live View

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

    #region Camera Settings

    public string GetShutterSpeed()
    {
        if (!TryGetUInt32Property(EdsPropertyID.PropID_Tv, out var tvValue))
            return "N/A";

        return ConvertTvToString(tvValue);
    }

    public string GetAperture()
    {
        if (!TryGetUInt32Property(EdsPropertyID.PropID_Av, out var avValue))
            return "N/A";

        return ConvertAvToString(avValue);
    }

    public string GetIso()
    {
        if (!TryGetUInt32Property(EdsPropertyID.PropID_ISOSpeed, out var isoValue))
            return "N/A";

        return ConvertIsoToString(isoValue);
    }

    public bool IsAutoIso()
    {
        if (!TryGetUInt32Property(EdsPropertyID.PropID_ISOSpeed, out var isoValue))
            return false;

        return isoValue == 0x00; // 0x00 = Auto ISO
    }

    private string ConvertTvToString(uint tv)
    {
        // Canon Tv (Time Value) to shutter speed string
        // based on Canon EDSDK documentation
        return tv switch
        {
            0x0C => "Bulb",
            0x10 => "30\"",
            0x13 => "25\"",
            0x14 => "20\"",
            0x15 => "20\"",
            0x18 => "15\"",
            0x1B => "13\"",
            0x1C => "10\"",
            0x1D => "10\"",
            0x20 => "8\"",
            0x23 => "6\"",
            0x24 => "6\"",
            0x25 => "5\"",
            0x28 => "4\"",
            0x2B => "3.2\"",
            0x2C => "3\"",
            0x2D => "2.5\"",
            0x30 => "2\"",
            0x33 => "1.6\"",
            0x34 => "1.5\"",
            0x35 => "1.3\"",
            0x38 => "1\"",
            0x3B => "0.8\"",
            0x3C => "0.7\"",
            0x3D => "0.6\"",
            0x40 => "0.5\"",
            0x43 => "0.4\"",
            0x44 => "0.3\"",
            0x45 => "0.3\"",
            0x48 => "1/4",
            0x4B => "1/5",
            0x4C => "1/6",
            0x4D => "1/6",
            0x50 => "1/8",
            0x53 => "1/10",
            0x54 => "1/10",
            0x55 => "1/13",
            0x58 => "1/15",
            0x5B => "1/20",
            0x5C => "1/20",
            0x5D => "1/25",
            0x60 => "1/30",
            0x63 => "1/40",
            0x64 => "1/45",
            0x65 => "1/50",
            0x68 => "1/60",
            0x6B => "1/80",
            0x6C => "1/90",
            0x6D => "1/100",
            0x70 => "1/125",
            0x73 => "1/160",
            0x74 => "1/180",
            0x75 => "1/200",
            0x78 => "1/250",
            0x7B => "1/320",
            0x7C => "1/350",
            0x7D => "1/400",
            0x80 => "1/500",
            0x83 => "1/640",
            0x84 => "1/750",
            0x85 => "1/800",
            0x88 => "1/1000",
            0x8B => "1/1250",
            0x8C => "1/1500",
            0x8D => "1/1600",
            0x90 => "1/2000",
            0x93 => "1/2500",
            0x94 => "1/3000",
            0x95 => "1/3200",
            0x98 => "1/4000",
            0x9B => "1/5000",
            0x9C => "1/6000",
            0x9D => "1/6400",
            0xA0 => "1/8000",
            _ => $"0x{tv:X}",
        };
    }

    private string ConvertAvToString(uint av)
    {
        // Canon Av (Aperture Value) to f-stop string
        // based on Canon EDSDK documentation
        return av switch
        {
            0x08 => "f/1.0",
            0x0B => "f/1.1",
            0x0C => "f/1.2",
            0x0D => "f/1.2",
            0x10 => "f/1.4",
            0x13 => "f/1.6",
            0x14 => "f/1.8",
            0x15 => "f/1.8",
            0x18 => "f/2.0",
            0x1B => "f/2.2",
            0x1C => "f/2.5",
            0x1D => "f/2.5",
            0x20 => "f/2.8",
            0x23 => "f/3.2",
            0x24 => "f/3.5",
            0x25 => "f/3.5",
            0x28 => "f/4.0",
            0x2B => "f/4.5",
            0x2C => "f/4.5",
            0x2D => "f/5.0",
            0x30 => "f/5.6",
            0x33 => "f/6.3",
            0x34 => "f/6.7",
            0x35 => "f/7.1",
            0x38 => "f/8.0",
            0x3B => "f/9.0",
            0x3C => "f/9.5",
            0x3D => "f/10",
            0x40 => "f/11",
            0x43 => "f/13",
            0x44 => "f/13",
            0x45 => "f/14",
            0x48 => "f/16",
            0x4B => "f/18",
            0x4C => "f/19",
            0x4D => "f/20",
            0x50 => "f/22",
            0x53 => "f/25",
            0x54 => "f/27",
            0x55 => "f/29",
            0x58 => "f/32",
            0x5B => "f/36",
            0x5C => "f/38",
            0x5D => "f/40",
            0x60 => "f/45",
            0x63 => "f/51",
            0x64 => "f/54",
            0x65 => "f/57",
            0x68 => "f/64",
            0x6B => "f/72",
            0x6C => "f/76",
            0x6D => "f/80",
            0x70 => "f/91",
            _ => $"0x{av:X}",
        };
    }

    private string ConvertIsoToString(uint iso)
    {
        // Canon ISO values to ISO string
        // based on Canon EDSDK documentation
        return iso switch
        {
            0x00 => "Auto",
            0x28 => "6",
            0x30 => "12",
            0x38 => "25",
            0x40 => "50",
            0x48 => "100",
            0x4B => "125",
            0x4D => "160",
            0x50 => "200",
            0x53 => "250",
            0x55 => "320",
            0x58 => "400",
            0x5B => "500",
            0x5D => "640",
            0x60 => "800",
            0x63 => "1000",
            0x65 => "1250",
            0x68 => "1600",
            0x6B => "2000",
            0x6D => "2500",
            0x70 => "3200",
            0x73 => "4000",
            0x75 => "5000",
            0x78 => "6400",
            0x7B => "8000",
            0x7D => "10000",
            0x80 => "12800",
            0x83 => "16000",
            0x85 => "20000",
            0x88 => "25600",
            0x8B => "32000",
            0x8D => "40000",
            0x90 => "51200",
            0x93 => "64000",
            0x95 => "80000",
            0x98 => "102400",
            _ => $"0x{iso:X}",
        };
    }

    #endregion Camera Settings

    #region Property Management

    public bool GetPropertyDesc(uint propertyId, out EdsPropertyDesc desc)
    {
        return EDSDK.EdsGetPropertyDesc(_camera, propertyId, out desc) == EdsError.EDS_ERR_OK;
    }

    public bool SetProperty(uint propertyId, uint value)
    {
        return TrySetUInt32Property(propertyId, value);
    }

    public bool GetAvailablePropertyValues(uint propertyId, out uint[] values)
    {
        if (GetPropertyDesc(propertyId, out var desc) && desc.NumElements > 0)
        {
            values = new uint[desc.NumElements];
            for (int i = 0; i < desc.NumElements; i++)
            {
                values[i] = (uint)desc.PropDesc[i];
            }
            return true;
        }

        values = Array.Empty<uint>();
        return false;
    }

    public bool GetNextPropertyValue(uint propertyId, out uint nextValue)
    {
        nextValue = 0;

        if (!TryGetUInt32Property(propertyId, out var currentValue))
            return false;

        if (
            !GetAvailablePropertyValues(propertyId, out var availableValues)
            || availableValues.Length == 0
        )
            return false;

        // find current value index
        int currentIndex = Array.IndexOf(availableValues, currentValue);
        if (currentIndex == -1)
        {
            // current value not in list, return first available
            nextValue = availableValues[0];
            return true;
        }

        // get next value (wrap around to start)
        int nextIndex = (currentIndex + 1) % availableValues.Length;
        nextValue = availableValues[nextIndex];
        return true;
    }

    public bool GetPreviousPropertyValue(uint propertyId, out uint prevValue)
    {
        prevValue = 0;

        if (!TryGetUInt32Property(propertyId, out var currentValue))
            return false;

        if (
            !GetAvailablePropertyValues(propertyId, out var availableValues)
            || availableValues.Length == 0
        )
            return false;

        // find current value index
        int currentIndex = Array.IndexOf(availableValues, currentValue);
        if (currentIndex == -1)
        {
            // current value not in list, return last available
            prevValue = availableValues[^1];
            return true;
        }

        // get previous value (wrap around to end)
        int prevIndex = currentIndex == 0 ? availableValues.Length - 1 : currentIndex - 1;
        prevValue = availableValues[prevIndex];
        return true;
    }

    #endregion Property Management

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

    #region Event Handlers and Download

    private uint OnObjectEvent(uint inEvent, IntPtr inRef, IntPtr inContext)
    {
        // defensive check: if camera is already closed, just release the reference and return
        if (_camera == IntPtr.Zero)
        {
            Console.WriteLine("OnObjectEvent called but camera is closed - releasing reference");
            if (inRef != IntPtr.Zero)
            {
                EDSDK.EdsRelease(inRef);
            }
            return (uint)EdsError.EDS_ERR_OK;
        }

        Console.WriteLine($"OnObjectEvent called: event=0x{inEvent:X8}, ref={inRef}");

        if (inEvent == EdsObjectEvent.DirItemRequestTransfer)
        {
            Console.WriteLine("DirItemRequestTransfer event detected - starting download");
            DownloadImage(inRef);
        }
        else if (inRef != IntPtr.Zero)
        {
            Console.WriteLine($"Other event (0x{inEvent:X8}) - releasing reference");
            EDSDK.EdsRelease(inRef);
        }
        return (uint)EdsError.EDS_ERR_OK;
    }

    private void DownloadImage(IntPtr directoryItem)
    {
        try
        {
            // get file info
            var err = EDSDK.EdsGetDirectoryItemInfo(directoryItem, out var dirItemInfo);
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"Failed to get directory item info: {err}");
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // create save directory if needed
            if (!string.IsNullOrEmpty(SavePath) && !System.IO.Directory.Exists(SavePath))
            {
                System.IO.Directory.CreateDirectory(SavePath);
            }

            // build full file path
            string filePath = System.IO.Path.Combine(SavePath, dirItemInfo.szFileName);
            Console.WriteLine($"Downloading image to: {filePath}");

            // create file stream
            err = EDSDK.EdsCreateFileStream(
                filePath,
                EdsFileCreateDisposition.CreateAlways,
                EdsAccess.ReadWrite,
                out var stream
            );
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"Failed to create file stream: {err}");
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // download image
            err = EDSDK.EdsDownload(directoryItem, dirItemInfo.Size, stream);
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"Failed to download image: {err}");
                EDSDK.EdsRelease(stream);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // complete download
            err = EDSDK.EdsDownloadComplete(directoryItem);
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"Failed to complete download: {err}");
            }

            // release resources
            EDSDK.EdsRelease(stream);
            EDSDK.EdsRelease(directoryItem);

            Console.WriteLine($"Successfully downloaded: {dirItemInfo.szFileName}");
        }
        catch (Exception ex)
        {
            // log error but don't crash
            Console.WriteLine($"Download error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (directoryItem != IntPtr.Zero)
            {
                EDSDK.EdsRelease(directoryItem);
            }
        }
    }

    #endregion Event Handlers and Download
}
