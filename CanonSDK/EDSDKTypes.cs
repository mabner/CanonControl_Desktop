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

#region Live View

public static class EdsPropertyID
{
    public const uint Evf_OutputDevice = 0x00000500;
}

public static class EdsOutputDevice
{
    public const uint PC = 2;
}

#endregion Live View

#region Focus Control

public static class EdsCameraCommand
{
    public const uint DriveLensEvf = 0x00000103;
    public const uint DoEvfAf = 0x00000102;
    public const uint TakePicture = 0x00000000;
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

#endregion Focus Control
