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
    private readonly object _downloadLock = new();

    // tracks how many transfer requests are still being processed.
    private int _pendingDownloads;

    public string? LastError { get; private set; }
    public bool LastConnectionAttemptFoundNoCamera { get; private set; }

    public string SavePath { get; set; } = string.Empty;
    public SaveDestination SaveDestination { get; set; } = SaveDestination.Camera;

    #region Initialization and Camera Management

    public bool Initialize()
    {
        var err = EDSDK.EdsInitializeSDK();
        if (err == EdsError.EDS_ERR_OK)
        {
            LastError = null;
            LastConnectionAttemptFoundNoCamera = false;
            return true;
        }

        LastConnectionAttemptFoundNoCamera = false;
        LastError = $"Failed to initialize Canon SDK: {err} (0x{(uint)err:X8})";
        Console.WriteLine($"[Connect] {LastError}");
        return false;
    }

    public bool ConnectFirstCamera()
    {
        LastError = null;
        LastConnectionAttemptFoundNoCamera = false;

        // close any existing connection first
        if (_camera != IntPtr.Zero)
        {
            EDSDK.EdsCloseSession(_camera);
            EDSDK.EdsRelease(_camera);
            _camera = IntPtr.Zero;
        }

        IntPtr cameraList;

        // get fresh camera list (detects newly connected cameras)
        var cameraListErr = EDSDK.EdsGetCameraList(out cameraList);
        if (cameraListErr != EdsError.EDS_ERR_OK)
        {
            LastError =
                $"Failed to enumerate cameras: {cameraListErr} (0x{(uint)cameraListErr:X8})";
            Console.WriteLine($"[Connect] {LastError}");
            return false;
        }

        try
        {
            int count;
            var childCountErr = EDSDK.EdsGetChildCount(cameraList, out count);
            if (childCountErr != EdsError.EDS_ERR_OK)
            {
                LastError =
                    $"Failed to query camera count: {childCountErr} (0x{(uint)childCountErr:X8})";
                Console.WriteLine($"[Connect] {LastError}");
                return false;
            }

            if (count == 0)
            {
                LastConnectionAttemptFoundNoCamera = true;
                LastError = "No camera detected.";
                Console.WriteLine("[Connect] No camera detected.");
                return false;
            }

            // get camera reference
            var childErr = EDSDK.EdsGetChildAtIndex(cameraList, 0, out _camera);
            if (childErr != EdsError.EDS_ERR_OK)
            {
                LastError = $"Failed to get camera reference: {childErr} (0x{(uint)childErr:X8})";
                Console.WriteLine($"[Connect] {LastError}");
                return false;
            }

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
                LastError = $"Failed to register object event handler: {err} (0x{(uint)err:X8})";
                Console.WriteLine($"[Connect] {LastError}");
                EDSDK.EdsRelease(_camera);
                _camera = IntPtr.Zero;
                _objectEventHandler = null;
                return false;
            }

            // NOW open session (after event handler registration)
            var openSessionErr = EDSDK.EdsOpenSession(_camera);
            if (openSessionErr != EdsError.EDS_ERR_OK)
            {
                LastError =
                    $"Failed to open camera session: {openSessionErr} (0x{(uint)openSessionErr:X8})";
                Console.WriteLine($"[Connect] {LastError}");

                // unregister handler on failure
                EDSDK.EdsSetObjectEventHandler(_camera, EdsObjectEvent.All, null, IntPtr.Zero);
                EDSDK.EdsRelease(_camera);
                _camera = IntPtr.Zero;
                _objectEventHandler = null;
                return false;
            }
            ApplySaveDestinationToCamera();

            LastError = null;
            Console.WriteLine("Camera connected successfully with event handlers registered");
            return true;
        }
        finally
        {
            EDSDK.EdsRelease(cameraList);
        }
    }

    public void ApplySaveDestination()
    {
        if (_camera != IntPtr.Zero)
            ApplySaveDestinationToCamera();
    }

    private void ApplySaveDestinationToCamera()
    {
        if (_camera == IntPtr.Zero)
            return;

        // use this.SaveDestination to disambiguate the property from the type.
        var dest = this.SaveDestination;
        uint saveToValue = (uint)dest;
        var saveToErr = SetUInt32PropertyWithRetry(EdsPropertyID.PropID_SaveTo, ref saveToValue);

        if (saveToErr == EdsError.EDS_ERR_OK)
        {
            if (dest != SaveDestination.Camera)
            {
                // EdsSetCapacity is required when the host is a save target.
                // report a large free-space value (~2 GB).
                var capacity = new EdsCapacity
                {
                    NumberOfFreeClusters = 0x7FFFFFFF,
                    BytesPerSector = 0x1000,
                    Reset = 1,
                };
                var capErr = EDSDK.EdsSetCapacity(_camera, capacity);
                Console.WriteLine(
                    $"Camera configured: SaveTo={dest}, SetCapacity result: {capErr}"
                );
            }
            else
            {
                Console.WriteLine($"Camera configured: SaveTo={dest}");
            }
        }
        else
        {
            Console.WriteLine(
                $"Warning: Failed to set PropID_SaveTo (err={saveToErr}). Images may not download automatically."
            );
        }
    }

    private EdsError SetUInt32PropertyWithRetry(
        uint propertyId,
        ref uint value,
        int maxRetries = 10
    )
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var err = EDSDK.EdsSetPropertyData(_camera, propertyId, 0, sizeof(uint), ref value);
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

    #region Folder Management

    // enumerates all folders on the camera's primary memory card volume.
    // retrieves folder information (name, path) suitable for UI selection.
    public List<CameraFolderInfo> EnumerateCameraFolders()
    {
        var folders = new List<CameraFolderInfo>();

        if (_camera == IntPtr.Zero)
            return folders;

        // get the camera's volume (memory card)
        IntPtr volume = IntPtr.Zero;
        try
        {
            var volErr = EDSDK.EdsGetChildAtIndex(_camera, 0, out volume);
            if (volErr != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Folders] Failed to get camera volume: {volErr}");
                return folders;
            }

            // find the DCIM folder and enumerate only its photo subfolders
            int rootFolderCount = 0;
            var countErr = EDSDK.EdsGetChildCount(volume, out rootFolderCount);
            if (countErr != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Folders] Failed to get folder count: {countErr}");
                return folders;
            }

            IntPtr dcimFolder = IntPtr.Zero;
            try
            {
                for (int i = 0; i < rootFolderCount && dcimFolder == IntPtr.Zero; i++)
                {
                    IntPtr folderRef = IntPtr.Zero;
                    try
                    {
                        var getErr = EDSDK.EdsGetChildAtIndex(volume, i, out folderRef);
                        if (getErr != EdsError.EDS_ERR_OK)
                            continue;

                        var infoErr = EDSDK.EdsGetDirectoryItemInfo(folderRef, out var info);
                        if (infoErr != EdsError.EDS_ERR_OK)
                            continue;

                        if (
                            info.isFolder
                            && string.Equals(
                                info.szFileName,
                                "DCIM",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            dcimFolder = folderRef;
                            folderRef = IntPtr.Zero; // ownership moves to dcimFolder
                        }
                    }
                    finally
                    {
                        if (folderRef != IntPtr.Zero)
                            EDSDK.EdsRelease(folderRef);
                    }
                }

                if (dcimFolder != IntPtr.Zero)
                {
                    int dcimFolderCount = 0;
                    var dcimCountErr = EDSDK.EdsGetChildCount(dcimFolder, out dcimFolderCount);
                    if (dcimCountErr != EdsError.EDS_ERR_OK)
                    {
                        Console.WriteLine(
                            $"[Folders] Failed to get DCIM child count: {dcimCountErr}"
                        );
                        return folders;
                    }

                    for (int i = 0; i < dcimFolderCount; i++)
                    {
                        IntPtr subfolderRef = IntPtr.Zero;
                        try
                        {
                            var getErr = EDSDK.EdsGetChildAtIndex(dcimFolder, i, out subfolderRef);
                            if (getErr != EdsError.EDS_ERR_OK)
                                continue;

                            var infoErr = EDSDK.EdsGetDirectoryItemInfo(subfolderRef, out var info);
                            if (infoErr != EdsError.EDS_ERR_OK)
                                continue;

                            if (info.isFolder)
                            {
                                folders.Add(
                                    new CameraFolderInfo
                                    {
                                        FolderName = info.szFileName,
                                        FolderPath = $"/DCIM/{info.szFileName}",
                                    }
                                );
                            }
                        }
                        finally
                        {
                            if (subfolderRef != IntPtr.Zero)
                                EDSDK.EdsRelease(subfolderRef);
                        }
                    }
                }
                else
                {
                    // fallback: enumerate top-level folders if no DCIM folder exists
                    for (int i = 0; i < rootFolderCount; i++)
                    {
                        IntPtr folderRef = IntPtr.Zero;
                        try
                        {
                            var getErr = EDSDK.EdsGetChildAtIndex(volume, i, out folderRef);
                            if (getErr != EdsError.EDS_ERR_OK)
                                continue;

                            var infoErr = EDSDK.EdsGetDirectoryItemInfo(folderRef, out var info);
                            if (infoErr != EdsError.EDS_ERR_OK)
                                continue;

                            if (
                                info.isFolder
                                && !string.Equals(
                                    info.szFileName,
                                    "MISC",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                folders.Add(
                                    new CameraFolderInfo
                                    {
                                        FolderName = info.szFileName,
                                        FolderPath = $"/{info.szFileName}",
                                    }
                                );
                            }
                        }
                        finally
                        {
                            if (folderRef != IntPtr.Zero)
                                EDSDK.EdsRelease(folderRef);
                        }
                    }
                }
            }
            finally
            {
                if (dcimFolder != IntPtr.Zero)
                    EDSDK.EdsRelease(dcimFolder);
            }
        }
        finally
        {
            if (volume != IntPtr.Zero)
                EDSDK.EdsRelease(volume);
        }

        return folders;
    }

    // creates a new folder on the camera's memory card.
    // the camera applies its standard naming convention (e.g., 100CANON, 101CANON).
    public EdsError CreateCameraFolder()
    {
        if (_camera == IntPtr.Zero)
        {
            Console.WriteLine("[Folders] Cannot create folder: no camera connected");
            return EdsError.EDS_ERR_DEVICE_BUSY;
        }

        // temporarily set SaveTo to Camera for folder creation
        var originalSaveTo = SaveDestination;
        SaveDestination = SaveDestination.Camera;
        ApplySaveDestinationToCamera();

        var err = RetryIfDeviceBusy(() => EDSDK.EdsCreateFolder(_camera));

        // restore original SaveTo
        SaveDestination = originalSaveTo;
        ApplySaveDestinationToCamera();

        if (err == EdsError.EDS_ERR_OK)
        {
            Console.WriteLine("[Folders] New folder created successfully");

            // pump EDSDK events for a short window so creation events are delivered
            var stopAt = DateTime.UtcNow.AddMilliseconds(500);
            while (DateTime.UtcNow < stopAt)
            {
                EDSDK.EdsGetEvent();
                Thread.Sleep(50);
            }

            return EdsError.EDS_ERR_OK;
        }

        Console.WriteLine($"[Folders] Failed to create folder: {err}");
        return err;
    }

    #endregion Folder Management

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

        // allocate memory buffer
        EDSDK.EdsCreateMemoryStream(0, out stream);

        // create EVF image reference
        EDSDK.EdsCreateEvfImageRef(stream, out evfImage);

        // download frame from camera
        var err = EDSDK.EdsDownloadEvfImage(_camera, evfImage);

        if (err != EdsError.EDS_ERR_OK)
        {
            EDSDK.EdsRelease(evfImage);
            EDSDK.EdsRelease(stream);

            // smart retry on DEVICE_BUSY
            if ((uint)err == 0x00000081) // DEVICE_BUSY
            {
                Thread.Sleep(50);
                return null;
            }

            return null;
        }

        // get pointer to buffer data
        EDSDK.EdsGetPointer(stream, out var pointer);

        // get buffer length
        EDSDK.EdsGetLength(stream, out var length);

        // copy to managed byte array
        byte[] data = new byte[length];
        Marshal.Copy(pointer, data, 0, (int)length);

        // release SDK resources
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

        // release SDK resources
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

        return FormatPropertyValue(EdsPropertyID.PropID_Tv, tvValue);
    }

    public string GetAperture()
    {
        if (!TryGetUInt32Property(EdsPropertyID.PropID_Av, out var avValue))
            return "N/A";

        return FormatPropertyValue(EdsPropertyID.PropID_Av, avValue);
    }

    public string GetIso()
    {
        if (!TryGetUInt32Property(EdsPropertyID.PropID_ISOSpeed, out var isoValue))
            return "N/A";

        return FormatPropertyValue(EdsPropertyID.PropID_ISOSpeed, isoValue);
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

    public bool TryGetPropertyValue(uint propertyId, out uint value)
    {
        return TryGetUInt32Property(propertyId, out value);
    }

    public string FormatPropertyValue(uint propertyId, uint value)
    {
        return propertyId switch
        {
            EdsPropertyID.PropID_Tv => ConvertTvToString(value),
            EdsPropertyID.PropID_Av => ConvertAvToString(value),
            EdsPropertyID.PropID_ISOSpeed => ConvertIsoToString(value),
            _ => $"0x{value:X}",
        };
    }

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

    public bool TryGetShiftedPropertyValue(
        uint propertyId,
        uint baseValue,
        double stopOffset,
        out uint targetValue
    )
    {
        targetValue = baseValue;

        if (
            !GetAvailablePropertyValues(propertyId, out var availableValues)
            || availableValues.Length == 0
        )
        {
            return false;
        }

        return CanonPropertyValueResolver.TryResolveShiftedValue(
            propertyId,
            baseValue,
            stopOffset,
            availableValues,
            out targetValue
        );
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
        if (_camera == IntPtr.Zero)
        {
            value = 0;
            return false;
        }

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
        if (_camera == IntPtr.Zero)
            return EdsError.EDS_ERR_OK;

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

    public void PumpEvents()
    {
        // some camera models require explicit event pumping so object events are delivered reliably
        EDSDK.EdsGetEvent();
    }

    public bool WaitForPendingDownloads(TimeSpan timeout, int pollIntervalMs = 50)
    {
        // wait until all queued download callbacks finish or timeout expires.
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            // pumps Canon events so transfer callbacks continue running.
            PumpEvents();

            if (Volatile.Read(ref _pendingDownloads) == 0)
            {
                return true;
            }

            Thread.Sleep(pollIntervalMs);
        }

        return Volatile.Read(ref _pendingDownloads) == 0;
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

        Console.WriteLine(
            $"[SDK] Camera object event received: event=0x{inEvent:X8} (0x208=ImageReadyToDownload, 0x201=VolumeInfo, 0x204=ItemCreated), ref={inRef}"
        );

        if (
            inEvent == EdsObjectEvent.DirItemRequestTransfer
            || inEvent == EdsObjectEvent.DirItemRequestTransferDT
        )
        {
            // increments pending transfer count for this incoming download request.
            Interlocked.Increment(ref _pendingDownloads);

            Console.WriteLine(
                $"Transfer request event detected (0x{inEvent:X8}) - starting download"
            );
            lock (_downloadLock)
            {
                DownloadImage(inRef);
            }
        }
        else if (inEvent == EdsObjectEvent.DirItemCreated)
        {
            Console.WriteLine(
                $"Folder/item created event detected (0x{inEvent:X8}) - releasing reference"
            );
            if (inRef != IntPtr.Zero)
            {
                EDSDK.EdsRelease(inRef);
            }
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
            if (this.SaveDestination == SaveDestination.Camera)
            {
                Console.WriteLine(
                    "[Download] Skipping host download because SaveDestination=Camera."
                );
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // get file info
            EdsDirectoryItemInfo dirItemInfo = default;
            var err = RetryIfDeviceBusy(() =>
                EDSDK.EdsGetDirectoryItemInfo(directoryItem, out dirItemInfo)
            );
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Download] Failed to get directory item info: {err}");
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // validate and prepare save path
            if (string.IsNullOrWhiteSpace(SavePath))
            {
                Console.WriteLine(
                    $"[Download] ERROR: SavePath is empty or null. Image '{dirItemInfo.szFileName}' cannot be downloaded."
                );
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // create save directory if needed
            if (!System.IO.Directory.Exists(SavePath))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(SavePath);
                    Console.WriteLine($"[Download] Created directory: {SavePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Download] Failed to create directory '{SavePath}': {ex.Message}"
                    );
                    EDSDK.EdsDownloadCancel(directoryItem);
                    EDSDK.EdsRelease(directoryItem);
                    return;
                }
            }

            // build full file path
            var fileName = string.IsNullOrWhiteSpace(dirItemInfo.szFileName)
                ? $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
                : dirItemInfo.szFileName;
            string filePath = System.IO.Path.Combine(SavePath, fileName);
            Console.WriteLine($"[Download] Target path: {filePath}");
            Console.WriteLine($"[Download] File size: {dirItemInfo.Size} bytes");

            // use an SDK memory stream and persist with .NET.
            // this avoids Windows path/ANSI issues in EdsCreateFileStream.
            IntPtr stream = IntPtr.Zero;
            err = RetryIfDeviceBusy(() => EDSDK.EdsCreateMemoryStream(0, out stream));
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine(
                    $"[Download] Failed to create memory stream for '{filePath}': {err}"
                );
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // download image
            err = RetryIfDeviceBusy(() =>
                EDSDK.EdsDownload(directoryItem, dirItemInfo.Size, stream)
            );
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Download] Failed to download image: {err}");
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(stream);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            IntPtr pointer = IntPtr.Zero;
            uint length = 0;
            err = RetryIfDeviceBusy(() => EDSDK.EdsGetPointer(stream, out pointer));
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Download] Failed to get memory stream pointer: {err}");
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(stream);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            err = RetryIfDeviceBusy(() => EDSDK.EdsGetLength(stream, out length));
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Download] Failed to get memory stream length: {err}");
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(stream);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            var data = new byte[length];
            Marshal.Copy(pointer, data, 0, (int)length);

            try
            {
                System.IO.File.WriteAllBytes(filePath, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Download] Failed to write file '{filePath}': {ex.Message}");
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(stream);
                EDSDK.EdsRelease(directoryItem);
                return;
            }

            // complete download
            err = RetryIfDeviceBusy(() => EDSDK.EdsDownloadComplete(directoryItem));
            if (err != EdsError.EDS_ERR_OK)
            {
                Console.WriteLine($"[Download] Failed to complete download: {err}");
                EDSDK.EdsDownloadCancel(directoryItem);
            }

            // release resources
            EDSDK.EdsRelease(stream);
            EDSDK.EdsRelease(directoryItem);

            // verify file exists and log final status
            if (System.IO.File.Exists(filePath))
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                Console.WriteLine(
                    $"[Download] OK: Successfully saved: {dirItemInfo.szFileName} ({fileInfo.Length} bytes) to {SavePath}"
                );
            }
            else
            {
                Console.WriteLine($"[Download] ERROR: File not found after download: {filePath}");
            }
        }
        catch (Exception ex)
        {
            // log error but don't crash
            Console.WriteLine($"[Download] Exception: {ex.Message}");
            Console.WriteLine($"[Download] Stack trace: {ex.StackTrace}");
            if (directoryItem != IntPtr.Zero)
            {
                EDSDK.EdsDownloadCancel(directoryItem);
                EDSDK.EdsRelease(directoryItem);
            }
        }
        finally
        {
            // decrements pending transfer count when this download request finishes.
            Interlocked.Decrement(ref _pendingDownloads);
        }
    }

    private EdsError RetryIfDeviceBusy(
        Func<EdsError> action,
        int maxRetries = 10,
        int delayMs = 100
    )
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var err = action();
            if (err == EdsError.EDS_ERR_OK)
                return err;

            if (err != EdsError.EDS_ERR_DEVICE_BUSY)
                return err;

            Thread.Sleep(delayMs);
            EDSDK.EdsGetEvent();
        }

        return EdsError.EDS_ERR_DEVICE_BUSY;
    }

    #endregion Event Handlers and Download
}
