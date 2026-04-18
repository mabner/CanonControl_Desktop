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

public static class EDSDK
{
#if WINDOWS
    private const string DLL = "EDSDK.dll";
#else
    private const string DLL = "EDSDK";
#endif

    [DllImport(DLL)]
    public static extern EdsError EdsInitializeSDK();

    [DllImport(DLL)]
    public static extern EdsError EdsTerminateSDK();

    [DllImport(DLL)]
    public static extern EdsError EdsGetCameraList(out IntPtr cameraList);

    [DllImport(DLL)]
    public static extern EdsError EdsGetChildCount(IntPtr listRef, out int count);

    [DllImport(DLL)]
    public static extern EdsError EdsGetChildAtIndex(IntPtr listRef, int index, out IntPtr camera);

    [DllImport(DLL)]
    public static extern EdsError EdsGetDeviceInfo(IntPtr camera, out EdsDeviceInfo deviceInfo);

    [DllImport(DLL)]
    public static extern EdsError EdsOpenSession(IntPtr camera);

    [DllImport(DLL)]
    public static extern EdsError EdsCloseSession(IntPtr camera);

    [DllImport(DLL)]
    public static extern EdsError EdsRelease(IntPtr obj);
}
