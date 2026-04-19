using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanonControl.ViewModels;

public partial class RemoteCaptureViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;

    [ObservableProperty]
    private string _shutterSpeed = string.Empty;

    [ObservableProperty]
    private string _aperture = string.Empty;

    [ObservableProperty]
    private string _iso = string.Empty;

    [ObservableProperty]
    private int _delaySeconds = 0;

    public RemoteCaptureViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;

        // initialize camera settings display with placeholder values
        UpdateCameraSettings();
    }

    public async Task TakeSinglePicture()
    {
        if (DelaySeconds > 0)
        {
            await Task.Delay(DelaySeconds * 1000);
        }

        _cameraService.TakePicture();
    }

    public void UpdateCameraSettings()
    {
        // TODO: implement camera settings polling
        // this requires:
        // 1. add property IDs to EDSDKTypes.cs:
        //    - propID_Tv = 0x00000406 (Shutter Speed/Time Value)
        //    - propID_Av = 0x00000405 (Aperture Value)
        //    - propID_ISOSpeed = 0x00000402
        // 2. add methods to EDSDKWrapper to get these properties
        // 3. add lookup tables to convert uint values to human-readable strings
        //    (e.g., 0x6D -> "1/100", 0x48 -> "f/5.6", 0x58 -> "ISO 400")
        // 4. call these methods here and update the observable properties

        // for now, display placeholder values when camera is connected
        ShutterSpeed = "1/100";
        Aperture = "f/5.6";
        Iso = "400";
    }
}
