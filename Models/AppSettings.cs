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
}
