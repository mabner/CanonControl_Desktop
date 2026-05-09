/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

namespace CanonControl.Models;

// represents a folder on the camera's memory card with metadata suitable for UI selection.
public class CameraFolderInfo
{
    public required string FolderName { get; set; }

    // the full path on the camera (e.g., "/DCIM/100CANON").
    public required string FolderPath { get; set; }
    public string DisplayName => $"{FolderName} ({FolderPath})";

    // initializes a new instance of the CameraFolderInfo class.
    public CameraFolderInfo() { }

    // initializes a new instance with folder name and path.
    public CameraFolderInfo(string folderName, string folderPath)
    {
        FolderName = folderName;
        FolderPath = folderPath;
    }

    public override string ToString() => DisplayName;
}
