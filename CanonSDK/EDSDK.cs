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
    #region Conditional Loading

#if WINDOWS
    private const string DLL = "EDSDK.dll";
#else
    private const string DLL = "EDSDK";
#endif

    #endregion Conditional Loading

    #region Event Handler Delegates

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint EdsObjectEventHandler(uint inEvent, IntPtr inRef, IntPtr inContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint EdsPropertyEventHandler(
        uint inEvent,
        uint inPropertyID,
        uint inParam,
        IntPtr inContext
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint EdsStateEventHandler(uint inEvent, uint inParam, IntPtr inContext);

    #endregion Event Handler Delegates

    # region Initialization and Camera Management
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

    [DllImport(DLL)]
    public static extern EdsError EdsGetEvent();

    [DllImport(DLL)]
    public static extern EdsError EdsSetCapacity(IntPtr camera, EdsCapacity capacity);

    # endregion# Initialization and Camera Management

    #region Event Handler Registration

    [DllImport(DLL)]
    public static extern EdsError EdsSetObjectEventHandler(
        IntPtr camera,
        uint inEvent,
        EdsObjectEventHandler? inObjectEventHandler,
        IntPtr inContext
    );

    [DllImport(DLL)]
    public static extern EdsError EdsSetPropertyEventHandler(
        IntPtr camera,
        uint inEvent,
        EdsPropertyEventHandler inPropertyEventHandler,
        IntPtr inContext
    );

    [DllImport(DLL)]
    public static extern EdsError EdsSetCameraStateEventHandler(
        IntPtr camera,
        uint inEvent,
        EdsStateEventHandler inStateEventHandler,
        IntPtr inContext
    );

    #endregion Event Handler Registration

    #region Image Download

    [DllImport(DLL)]
    public static extern EdsError EdsGetDirectoryItemInfo(
        IntPtr inDirItemRef,
        out EdsDirectoryItemInfo outDirItemInfo
    );

    [DllImport(DLL)]
    public static extern EdsError EdsCreateFileStream(
        [MarshalAs(UnmanagedType.LPStr)] string inFileName,
        uint inCreateDisposition,
        uint inDesiredAccess,
        out IntPtr outStream
    );

    [DllImport(DLL)]
    public static extern EdsError EdsDownload(
        IntPtr inDirItemRef,
        ulong inReadSize,
        IntPtr outStream
    );

    [DllImport(DLL)]
    public static extern EdsError EdsDownloadComplete(IntPtr inDirItemRef);

    [DllImport(DLL)]
    public static extern EdsError EdsDownloadCancel(IntPtr inDirItemRef);

    #endregion Image Download

    # region Live View
    [DllImport(DLL)]
    public static extern EdsError EdsSetPropertyData(
        IntPtr camera,
        uint propertyID,
        int param,
        int size,
        ref uint data
    ); // turn on live view

    [DllImport(DLL)]
    public static extern EdsError EdsSetPropertyData(
        IntPtr camera,
        uint propertyID,
        int param,
        int size,
        uint data
    ); // overload for passing value directly

    [DllImport(DLL)]
    public static extern EdsError EdsGetPropertyData(
        IntPtr camera,
        uint propertyID,
        int param,
        int size,
        out uint data
    ); // get live view status

    [DllImport(DLL)]
    public static extern EdsError EdsGetPropertyData(
        IntPtr evfImageRef,
        uint propertyID,
        int param,
        int size,
        [Out] uint[] data
    );

    [DllImport(DLL)]
    public static extern EdsError EdsGetPropertySize(
        IntPtr evfImageRef,
        uint propertyID,
        int param,
        out int dataType,
        out int size
    );

    [DllImport(DLL)]
    public static extern EdsError EdsCreateMemoryStream(uint size, out IntPtr stream); // image buffer

    [DllImport(DLL)]
    public static extern EdsError EdsCreateEvfImageRef(IntPtr stream, out IntPtr evfImage); // frame reference

    [DllImport(DLL)]
    public static extern EdsError EdsDownloadEvfImage(IntPtr camera, IntPtr evfImage); // get the camera image

    [DllImport(DLL)]
    public static extern EdsError EdsGetPointer(IntPtr stream, out IntPtr pointer); // buffer access

    [DllImport(DLL)]
    public static extern EdsError EdsGetLength(IntPtr stream, out uint length); // buffer size
    # endregion Live View

    #region Focus Control

    [DllImport(DLL)]
    public static extern EdsError EdsSendCommand(IntPtr camera, uint command, int param);

    #endregion Focus Control

    #region Property Description

    [DllImport(DLL)]
    public static extern EdsError EdsGetPropertyDesc(
        IntPtr camera,
        uint propertyID,
        out EdsPropertyDesc propertyDesc
    );

    #endregion Property Description
}
