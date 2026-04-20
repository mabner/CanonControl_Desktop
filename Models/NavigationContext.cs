using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CanonControl.Models;

public enum NavigationContext
{
    RemoteCapture,
    FocusStack,
    ExposureBracketing,
    TimeLapse,
    Settings,
}
