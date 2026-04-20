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
using System.Threading.Tasks;

namespace CanonControl.CanonSDK;

public enum EdsError : uint
{
    EDS_ERR_OK = 0x00000000,
    EDS_ERR_DEVICE_BUSY = 0x00000081,
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsDeviceInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szPortName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szDeviceDescription;

    public uint deviceSubType;
    public uint reserved;
}

public struct EdsBaseRef
{
    public IntPtr Ref;
}

public struct EdsCameraRef
{
    public IntPtr Ref;
}

[StructLayout(LayoutKind.Sequential)]
public struct EdsPropertyDesc
{
    public int Form;
    public int Access;
    public int NumElements;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public int[] PropDesc;
}

#region Live View

public static class EdsPropertyID
{
    public const uint Evf_OutputDevice = 0x00000500;
    public const uint Evf_Mode = 0x00000501;

    // Camera settings properties
    public const uint PropID_ISOSpeed = 0x00000402;
    public const uint PropID_Av = 0x00000405; // Aperture Value
    public const uint PropID_Tv = 0x00000406; // Shutter Speed (Time Value)
    public const uint Evf_HistogramY = 0x00000515;
    public const uint Evf_HistogramR = 0x00000516;
    public const uint Evf_HistogramG = 0x00000517;
    public const uint Evf_HistogramB = 0x00000518;
    public const uint Evf_HistogramStatus = 0x0000050C;
}

public static class EdsOutputDevice
{
    public const uint TFT = 1;
    public const uint PC = 2;
    public const uint PCSmall = 8;
}

#endregion Live View

#region Focus Control

public static class EdsCameraCommand
{
    public const uint TakePicture = 0x00000000;
    public const uint PressShutterButton = 0x00000004;
    public const uint DoEvfAf = 0x00000102;
    public const uint DriveLensEvf = 0x00000103;
}

public static class EdsShutterButton
{
    // TODO: assign halfway to 'press and hold' the shutter button, and 'click' to completely
    public const int Off = 0x00000000;
    public const int Halfway = 0x00000001;
    public const int Completely = 0x00000003;
}

public static class EdsEvfDriveLens
{
    public const int Near1 = 0x00000001;
    public const int Near2 = 0x00000002;
    public const int Near3 = 0x00000003;

    public const int Far1 = 0x00008001;
    public const int Far2 = 0x00008002;
    public const int Far3 = 0x00008003;
}

public static class EdsEvfAf
{
    public const int CameraCommand_EvfAf_OFF = 0;
    public const int CameraCommand_EvfAf_ON = 1;

    // Backward-compatible aliases
    public const int ON = 1;
    public const int OFF = 0;
}

public static class EdsEvfHistogramStatus
{
    public const int Hide = 0;
    public const int Normal = 1;
    public const int Grayout = 2;
}

#endregion Focus Control
