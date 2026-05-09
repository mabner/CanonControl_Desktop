/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

namespace CanonControl.Models;

public enum ImageFormat : uint
{
    // Values extracted from Canon EDSDK sample
    JPEG = 0x0013FF0F,      // Large Fine Jpeg (EdsImageQuality_LJF)
    RAW = 0x0064FF0F,       // RAW (EdsImageQuality_LR)
    RAWAndJPEG = 0x00640013 // RAW + Large Fine Jpeg (EdsImageQuality_LRLJF)
}
