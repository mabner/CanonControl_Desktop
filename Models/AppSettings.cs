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
using System.Threading.Tasks;

namespace CanonControl.Models;

public class AppSettings
{
    public string SavePath { get; set; } = string.Empty;

    public SaveDestination SaveDestination { get; set; } = SaveDestination.Camera;

    public bool AutoDownload { get; set; } = false;

    public int LiveViewFrameRate { get; set; } = 30; // default to 30 FPS

    public bool LiveViewDuringAutoFocus { get; set; } = true;

    public int ConnectionTimeout { get; set; } = 10;

    // number of Near1/Far1 fine steps sent per Medium focus press
    public int FocusMediumSteps { get; set; } = 3;

    // number of Near1/Far1 fine steps sent per Coarse focus press
    public int FocusCoarseSteps { get; set; } = 6;

    // selected camera card folder name for image storage (e.g., "100CANON", "101CANON").
    // empty string means use camera default.
    public string SelectedCameraFolder { get; set; } = string.Empty;
}
