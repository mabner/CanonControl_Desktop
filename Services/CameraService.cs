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
    public event EventHandler<bool>? AutoFocusActiveChanged;
    private readonly EDSDKWrapper _sdk = new();
    private readonly object _cameraLock = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _focusCts;
    private Task? _liveViewTask;
    private volatile int _lastEvfFrameWidth = 0;
    private volatile int _lastEvfFrameHeight = 0;

    public CameraService()
    {
        try
        {
            NativeLibraryLoader.LoadEDSDK();
            _sdk.Initialize();
            _sdk.CameraAdded += (s, e) => CameraAdded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CameraService] Initial load failed: {ex.Message}");
        }
    }

    public event EventHandler? CameraAdded;
    private volatile bool _isEvfDownloadPaused = false;
    public string? LastConnectionError { get; private set; }
    public bool LastConnectionAttemptFoundNoCamera { get; private set; }

    #region Settings
    public bool LiveViewDuringAutoFocus { get; set; } = true;
    public int FocusMediumSteps { get; set; } = 3;
    public int FocusCoarseSteps { get; set; } = 6;

    // relative step counter: +1 per Far1 pulse, -1 per Near1 pulse; reset when point A is registered.
    public int FocusStepPosition { get; private set; } = 0;

    // resets the step counter to zero; called when the user registers focus point A.
    public void ResetFocusStepPosition()
    {
        lock (_cameraLock)
        {
            FocusStepPosition = 0;
        }
    }

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

    // the currently selected camera folder name for image storage (e.g., "100CANON").
    // empty string means use camera default.
    public string SelectedCameraFolder { get; set; } = string.Empty;

    #endregion Settings

    #region Connect and Startup

    public async Task<bool> ConnectAsync(int timeoutSeconds = 10)
    {
        LastConnectionError = null;
        LastConnectionAttemptFoundNoCamera = false;

        if (!_sdk.Initialize())
        {
            LastConnectionError = _sdk.LastError;
            return false;
        }

        // poll for camera connection with configurable timeout
        const int delayMs = 500;
        int maxAttempts = (timeoutSeconds * 1000) / delayMs; // Convert seconds to attempts

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_sdk.ConnectFirstCamera())
            {
                LastConnectionError = null;
                LastConnectionAttemptFoundNoCamera = false;
                return true;
            }

            LastConnectionError = _sdk.LastError;
            LastConnectionAttemptFoundNoCamera = _sdk.LastConnectionAttemptFoundNoCamera;

            if (!LastConnectionAttemptFoundNoCamera)
                return false;

            // wait before next attempt (non-blocking)
            await Task.Delay(delayMs);
        }

        LastConnectionError ??= "No camera detected.";
        LastConnectionAttemptFoundNoCamera = true;
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

    public int? GetBatteryPercentage()
    {
        lock (_cameraLock)
        {
            if (
                !_sdk.TryGetPropertyValue(
                    EdsPropertyID.PropID_BatteryLevel,
                    out var rawBatteryValue
                )
            )
                return null;

            // battery level is returned as 0-100% directly from the camera
            if (rawBatteryValue <= 100)
                return (int)rawBatteryValue;

            // higher values might indicate AC power or special states
            // return as is if within valid range
            return (int)rawBatteryValue;
        }
    }

    #endregion Connect and Startup


    #region Folder Management


    // retrieves a list of all folders on the camera's memory card.
    public List<CameraFolderInfo> GetAvailableCameraFolders()
    {
        lock (_cameraLock)
        {
            return _sdk.EnumerateCameraFolders();
        }
    }

    // creates a new folder on the camera's memory card.
    // the camera applies its standard naming convention (100CANON, 101CANON, etc).
    public EdsError CreateCameraFolder()
    {
        lock (_cameraLock)
        {
            return _sdk.CreateCameraFolder();
        }
    }

    #endregion Folder Management

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
                        {
                            // update last frame size for accurate click-to-AF mapping
                            try
                            {
                                (int w, int h) = GetJpegSize(frame);
                                if (w > 0 && h > 0)
                                {
                                    _lastEvfFrameWidth = w;
                                    _lastEvfFrameHeight = h;
                                }
                            }
                            catch { }

                            onFrame(frame);
                        }

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
        // serializes lens drive with live view and pauses EVF to avoid EDSDK contention.
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            try
            {
                _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
                FocusStepPosition--; // track relative position: near moves closer (negative direction).
                Thread.Sleep(50);
            }
            finally
            {
                _isEvfDownloadPaused = false;
            }
        }
    }

    public void FocusNearMedium()
    {
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            try
            {
                for (int i = 0; i < FocusMediumSteps; i++)
                {
                    _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
                    FocusStepPosition--; // decrement once per Near1 pulse.
                    Thread.Sleep(50);
                }
            }
            finally
            {
                _isEvfDownloadPaused = false;
            }
        }
    }

    public void FocusNearCoarse()
    {
        // serializes coarse lens steps with EVF the same way as fine focus.
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            try
            {
                for (int i = 0; i < FocusCoarseSteps; i++)
                {
                    _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
                    FocusStepPosition--; // decrement once per Near1 pulse.
                    Thread.Sleep(50);
                }
            }
            finally
            {
                _isEvfDownloadPaused = false;
            }
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
                        _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
                        FocusStepPosition--; // decrement on each continuous Near1 pulse.
                    }

                    await Task.Delay(80, token); // focus adjustment interval
                }
            },
            token
        );
    }

    public void FocusFarFine()
    {
        // focus stack uses this path; must not run concurrently with EVF downloads.
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            try
            {
                _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
                FocusStepPosition++; // track relative position: far moves away (positive direction).
                Thread.Sleep(50);
            }
            finally
            {
                _isEvfDownloadPaused = false;
            }
        }
    }

    public void FocusFarMedium()
    {
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            try
            {
                for (int i = 0; i < FocusMediumSteps; i++)
                {
                    _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
                    FocusStepPosition++; // increment once per Far1 pulse.
                    Thread.Sleep(50);
                }
            }
            finally
            {
                _isEvfDownloadPaused = false;
            }
        }
    }

    public void FocusFarCoarse()
    {
        lock (_cameraLock)
        {
            _isEvfDownloadPaused = true;
            try
            {
                for (int i = 0; i < FocusCoarseSteps; i++)
                {
                    _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
                    FocusStepPosition++; // increment once per Far1 pulse.
                    Thread.Sleep(50);
                }
            }
            finally
            {
                _isEvfDownloadPaused = false;
            }
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
                        _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
                        FocusStepPosition++; // increment on each continuous Far1 pulse.
                    }

                    await Task.Delay(80, token);
                }
            },
            token
        );
    }

    public void StartFocusNearMedium()
    {
        StopFocus();

        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < FocusMediumSteps; i++)
                    {
                        lock (_cameraLock)
                        {
                            _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
                            FocusStepPosition--; // decrement per Near1 pulse in continuous medium near.
                        }
                        await Task.Delay(50, token);
                    }
                    await Task.Delay(80, token);
                }
            },
            token
        );
    }

    public void StartFocusNearCoarse()
    {
        StopFocus();

        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < FocusCoarseSteps; i++)
                    {
                        lock (_cameraLock)
                        {
                            _sdk.DriveLensNear(EdsEvfDriveLens.Near1);
                            FocusStepPosition--; // decrement per Near1 pulse in continuous coarse near.
                        }
                        await Task.Delay(50, token);
                    }
                    await Task.Delay(80, token);
                }
            },
            token
        );
    }

    public void StartFocusFarMedium()
    {
        StopFocus();

        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < FocusMediumSteps; i++)
                    {
                        lock (_cameraLock)
                        {
                            _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
                            FocusStepPosition++; // increment per Far1 pulse in continuous medium far.
                        }
                        await Task.Delay(50, token);
                    }
                    await Task.Delay(80, token);
                }
            },
            token
        );
    }

    public void StartFocusFarCoarse()
    {
        StopFocus();

        _focusCts = new CancellationTokenSource();
        var token = _focusCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < FocusCoarseSteps; i++)
                    {
                        lock (_cameraLock)
                        {
                            _sdk.DriveLensFar(EdsEvfDriveLens.Far1);
                            FocusStepPosition++; // increment per Far1 pulse in continuous coarse far.
                        }
                        await Task.Delay(50, token);
                    }
                    await Task.Delay(80, token);
                }
            },
            token
        );
    }

    public void StartAutoFocus()
    {
        SetEvfAutoFocus(true);
        // notify listeners that autofocus is active
        try
        {
            AutoFocusActiveChanged?.Invoke(this, true);
        }
        catch { }
    }

    public void StopAutoFocus()
    {
        SetEvfAutoFocus(false);
        // notify listeners that autofocus stopped (focus may be locked)
        try
        {
            AutoFocusActiveChanged?.Invoke(this, false);
        }
        catch { }
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

            // pump EDSDK events for a short window so transfer callbacks are delivered
            var stopAt = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < stopAt)
            {
                lock (_cameraLock)
                {
                    _sdk.PumpEvents();
                }
                Thread.Sleep(50);
            }
            Console.WriteLine("[TakePicture] Initial event pump completed");

            // waits for host transfers to finish before allowing the next capture.
            var downloadDrained = _sdk.WaitForPendingDownloads(TimeSpan.FromSeconds(10));
            Console.WriteLine(
                downloadDrained
                    ? "[TakePicture] Pending downloads drained"
                    : "[TakePicture] Timeout waiting for pending downloads to drain"
            );
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

    // Positions the camera's AF frame to the clicked point and triggers a brief AF cycle.
    // Requires Live AF mode (FlexiZone); switches temporarily if camera is in Quick mode.
    public void ClickAfAtPoint(double xNormalized, double yNormalized)
    {
        // clamp inputs to valid normalised range
        xNormalized = Math.Max(0.0, Math.Min(1.0, xNormalized));
        yNormalized = Math.Max(0.0, Math.Min(1.0, yNormalized));

        // run off the UI thread to avoid blocking the live-view loop
        Task.Run(() =>
        {
            lock (_cameraLock)
            {
                _isEvfDownloadPaused = true;
            }

            uint originalAfMode = EdsEvfAfMode.Quick;
            bool didSwitchAfMode = false;

            try
            {
                // read current Evf_AFMode; SetFramePoint only moves the AF area in Live mode
                lock (_cameraLock)
                {
                    _sdk.TryGetPropertyValue(EdsPropertyID.PropID_Evf_AFMode, out originalAfMode);
                }

                if (originalAfMode == EdsEvfAfMode.Quick)
                {
                    // must stop any active DoEvfAf before changing mode (Canon sample requirement)
                    SetEvfAutoFocus(false);
                    Thread.Sleep(100);

                    lock (_cameraLock)
                    {
                        // switch to Live (FlexiZone) so SetFramePoint affects AF detection
                        _sdk.SetProperty(EdsPropertyID.PropID_Evf_AFMode, EdsEvfAfMode.Live);
                        didSwitchAfMode = true;
                        Console.WriteLine("[ClickAfAtPoint] Switched to Live AF mode for click-to-focus.");
                    }

                    Thread.Sleep(150); // allow camera to switch modes
                }

                // read the camera's coordinate system from the current EVF frame
                EdsSize coordSystem;
                bool hasCoordsystem;

                lock (_cameraLock)
                {
                    hasCoordsystem = _sdk.TryGetEvfCoordinateSystem(out coordSystem);
                }

                if (hasCoordsystem && coordSystem.width > 0 && coordSystem.height > 0)
                {
                    // map normalised click to camera coordinate space (JPEG-Large pixel coords)
                    var framePoint = new EdsSize
                    {
                        width  = (int)Math.Round(xNormalized * coordSystem.width),
                        height = (int)Math.Round(yNormalized * coordSystem.height),
                    };

                    // clamp to valid range
                    framePoint.width  = Math.Max(0, Math.Min(coordSystem.width  - 1, framePoint.width));
                    framePoint.height = Math.Max(0, Math.Min(coordSystem.height - 1, framePoint.height));

                    lock (_cameraLock)
                    {
                        // positions AF frame; lockAfFrame=true holds position until DoEvfAf fires
                        var err = _sdk.SetFramePoint(framePoint, lockAfFrame: true);
                        Console.WriteLine($"[ClickAfAtPoint] SetFramePoint({framePoint.width},{framePoint.height}) in {coordSystem.width}x{coordSystem.height} → {err}");
                    }
                }
                else
                {
                    Console.WriteLine("[ClickAfAtPoint] Could not read Evf_CoordinateSystem — AF will fire at current camera AF point.");
                }

                // trigger AF cycle at the new frame position
                SetEvfAutoFocus(true);
                Thread.Sleep(700);
                SetEvfAutoFocus(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClickAfAtPoint] Error: {ex.Message}");
            }
            finally
            {
                // restore original AF mode if we changed it
                if (didSwitchAfMode)
                {
                    lock (_cameraLock)
                    {
                        _sdk.SetProperty(EdsPropertyID.PropID_Evf_AFMode, originalAfMode);
                        Console.WriteLine($"[ClickAfAtPoint] Restored AF mode to {originalAfMode}.");
                    }
                }

                lock (_cameraLock)
                {
                    _isEvfDownloadPaused = false;
                }
            }
        });
    }

    // expose last EVF frame size for diagnostics
    public (int width, int height) GetLastEvfFrameSize() =>
        (_lastEvfFrameWidth, _lastEvfFrameHeight);

    // minimal JPEG SOF parser to extract width/height
    private static (int width, int height) GetJpegSize(byte[] jpeg)
    {
        if (jpeg == null || jpeg.Length < 4)
            return (0, 0);

        int i = 0;
        // check SOI
        if (jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return (0, 0);

        i = 2;
        while (i + 3 < jpeg.Length)
        {
            if (jpeg[i] != 0xFF)
            {
                i++;
                continue;
            }
            int marker = jpeg[i + 1] & 0xFF;
            // SOF0/1/2/3/5/6/7/9/10/11/13/14/15
            if (
                marker == 0xC0
                || marker == 0xC1
                || marker == 0xC2
                || marker == 0xC3
                || marker == 0xC5
                || marker == 0xC6
                || marker == 0xC7
                || marker == 0xC9
                || marker == 0xCA
                || marker == 0xCB
                || marker == 0xCD
                || marker == 0xCE
                || marker == 0xCF
            )
            {
                if (i + 7 >= jpeg.Length)
                    return (0, 0);
                // length = next two bytes
                int blockLength = (jpeg[i + 2] << 8) | jpeg[i + 3];
                // precision = jpeg[i+4]
                int height = (jpeg[i + 5] << 8) | jpeg[i + 6];
                int width = (jpeg[i + 7] << 8) | jpeg[i + 8];
                return (width, height);
            }
            else
            {
                if (i + 3 >= jpeg.Length)
                    return (0, 0);
                int blockLength = (jpeg[i + 2] << 8) | jpeg[i + 3];
                if (blockLength < 2)
                    return (0, 0);
                i += 2 + blockLength;
            }
        }

        return (0, 0);
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

    public ImageFormat GetImageFormat()
    {
        lock (_cameraLock)
        {
            if (_sdk.TryGetPropertyValue(EdsPropertyID.PropID_ImageQuality, out uint value))
            {
                return (ImageFormat)value;
            }
            return ImageFormat.JPEG; // Default fallback
        }
    }

    public void SetImageFormat(ImageFormat format)
    {
        lock (_cameraLock)
        {
            if (
                _sdk.GetAvailablePropertyValues(
                    EdsPropertyID.PropID_ImageQuality,
                    out uint[] available
                )
            )
            {
                Console.WriteLine("[ImageFormat] Available values:");
                foreach (var val in available)
                {
                    Console.WriteLine($"  - 0x{val:X8}");
                }
            }

            bool success = _sdk.SetProperty(EdsPropertyID.PropID_ImageQuality, (uint)format);
            Console.WriteLine($"[ImageFormat] Set {(uint)format:X8} success: {success}");
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
                var result = _sdk.SetProperty(EdsPropertyID.PropID_Tv, nextValue);
                if (result)
                    Thread.Sleep(200); // wait for camera to apply
                return result;
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
                var result = _sdk.SetProperty(EdsPropertyID.PropID_Tv, prevValue);
                if (result)
                    Thread.Sleep(200); // wait for camera to apply
                return result;
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
                var result = _sdk.SetProperty(EdsPropertyID.PropID_Av, nextValue);
                if (result)
                    Thread.Sleep(200); // wait for camera to apply
                return result;
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
                var result = _sdk.SetProperty(EdsPropertyID.PropID_Av, prevValue);
                if (result)
                    Thread.Sleep(200); // wait for camera to apply
                return result;
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
                var result = _sdk.SetProperty(EdsPropertyID.PropID_ISOSpeed, nextValue);
                if (result)
                    Thread.Sleep(200); // wait for camera to apply
                return result;
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
                var result = _sdk.SetProperty(EdsPropertyID.PropID_ISOSpeed, prevValue);
                if (result)
                    Thread.Sleep(200); // wait for camera to apply
                return result;
            }
            return false;
        }
    }

    #endregion Property Management

    #region Histogram

    public HistogramData? GetHistogramData()
    {
        // skips EVF histogram while capture/transfer runs to avoid EDSDK contention with file download.
        if (_isEvfDownloadPaused)
            return null;

        lock (_cameraLock)
        {
            return _sdk.GetHistogramData();
        }
    }

    #endregion Histogram
}
