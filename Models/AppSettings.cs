using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CanonControl.Models;

public class AppSettings
{
    public string SavePath { get; set; } = string.Empty;

    public bool AutoDownload { get; set; } = false;

    public int LiveViewFrameRate { get; set; } = 30; // default to 30 FPS

    public bool LiveViewDuringAutoFocus { get; set; } = true;
}
