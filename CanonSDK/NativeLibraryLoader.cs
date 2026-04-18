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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CanonControl.CanonSDK;

public static class NativeLibraryLoader
{
    public static void LoadEDSDK()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Load("Platforms/Windows/EDSDK.dll");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                // chmod +x libEDSDK.so
                Load("Platforms/Linux/x64/libEDSDK.so");
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                // chmod +x libEDSDK.so
                Load("Platforms/Linux/arm64/libEDSDK.so");
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported Linux architecture");
            }
        }
    }

    private static void Load(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Library not found: {fullPath}");

        NativeLibrary.Load(fullPath);
    }
}
