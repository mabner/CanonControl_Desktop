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
