/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

namespace CanonControl.Models;

public class HistogramData
{
    public uint[] Luminance { get; set; } = new uint[256];
    public uint[] Red { get; set; } = new uint[256];
    public uint[] Green { get; set; } = new uint[256];
    public uint[] Blue { get; set; } = new uint[256];
    public int Status { get; set; } // 0=Hide, 1=Normal, 2=Grayout

    public bool IsVisible => Status == 1; // Normal
    public bool IsGrayout => Status == 2;
}
