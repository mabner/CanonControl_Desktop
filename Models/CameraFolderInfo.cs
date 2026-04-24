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
