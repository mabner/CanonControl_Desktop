using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanonControl.CanonSDK;
using CanonControl.Models;
using CanonControl.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly CameraService _cameraService;
    private readonly AppSettings _settings;

    public SettingsViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        // initialize properties from loaded settings
        _savePath = _settings.SavePath;
        _autoDownload = _settings.AutoDownload;
        _liveViewFrameRate = _settings.LiveViewFrameRate;
        _liveViewDuringAutoFocus = _settings.LiveViewDuringAutoFocus;
        _connectionTimeout = _settings.ConnectionTimeout;
        _saveDestinationIndex = SaveDestinationToIndex(_settings.SaveDestination);
        _focusMediumSteps = _settings.FocusMediumSteps;
        _focusCoarseSteps = _settings.FocusCoarseSteps;
        _selectedCameraFolderName = _settings.SelectedCameraFolder;

        // keep active runtime session aligned with persisted settings when opening Settings panel.
        _cameraService.SavePath = _savePath;
        _cameraService.SaveDestination = _settings.SaveDestination;
        _cameraService.LiveViewDuringAutoFocus = _liveViewDuringAutoFocus;
        RefreshCameraFolders();
    }

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _savePath = string.Empty;

    partial void OnSavePathChanged(string value)
    {
        // apply immediately so next capture uses the selected folder even before pressing Save.
        _cameraService.SavePath = value;
    }

    [ObservableProperty]
    private bool _autoDownload = true;

    [ObservableProperty]
    private int _liveViewFrameRate = 30;

    [ObservableProperty]
    private bool _liveViewDuringAutoFocus = true;

    [ObservableProperty]
    private int _connectionTimeout = 10;

    [ObservableProperty]
    private int _focusMediumSteps = 3;

    partial void OnFocusMediumStepsChanged(int value)
    {
        _cameraService.FocusMediumSteps = value;
    }

    [ObservableProperty]
    private int _focusCoarseSteps = 6;

    partial void OnFocusCoarseStepsChanged(int value)
    {
        _cameraService.FocusCoarseSteps = value;
    }

    [ObservableProperty]
    private List<CameraFolderInfo> _availableFolders = new();

    [ObservableProperty]
    private string _selectedCameraFolderName = string.Empty;

    // comboBox index for selected folder binding
    [ObservableProperty]
    private int _selectedFolderIndex = -1;

    partial void OnSelectedFolderIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableFolders.Count)
        {
            SelectedCameraFolderName = AvailableFolders[value].FolderName;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHostSaveDestination))]
    private int _saveDestinationIndex = 0;

    partial void OnSaveDestinationIndexChanged(int value)
    {
        _cameraService.SaveDestination = IndexToSaveDestination(value);
    }

    // Human-readable labels for the save destination ComboBox
    public List<string> SaveDestinationOptions { get; } =
        new() { "Camera only", "PC only", "Camera + PC" };

    public bool HasHostSaveDestination =>
        IndexToSaveDestination(SaveDestinationIndex) != SaveDestination.Camera;

    // display the settings file path for user reference
    public string SettingsFilePath => _settingsService.GetSettingsPath();

    // available frame rate options
    public List<int> FrameRateOptions { get; } = new() { 15, 20, 30 };

    // available connection timeout options (in seconds)
    public List<int> ConnectionTimeoutOptions { get; } = new() { 5, 10, 15, 20, 30 };

    // Available focus steps options
    public List<int> FocusStepOptions { get; } =
        new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

    [RelayCommand]
    private async Task BrowseSavePath()
    {
        try
        {
            // get the main window to use as parent for the dialog
            if (
                Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            )
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    // use the modern StorageProvider API (Avalonia 11+)
                    var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(
                        new Avalonia.Platform.Storage.FolderPickerOpenOptions
                        {
                            Title = "Select Save Path",
                            AllowMultiple = false,
                        }
                    );

                    if (folders.Count > 0)
                    {
                        // get the local path from the storage folder
                        SavePath = folders[0].Path.LocalPath;
                    }
                }
            }
        }
        catch
        {
            // silently handle errors - user can type path manually
        }
    }

    // refresh the list of available folders from the currently connected camera.
    private void RefreshCameraFolders()
    {
        try
        {
            var folders = _cameraService.GetAvailableCameraFolders();
            AvailableFolders = folders;

            // try to find and select the previously saved folder
            if (!string.IsNullOrEmpty(SelectedCameraFolderName))
            {
                var index = folders.FindIndex(f => f.FolderName == SelectedCameraFolderName);
                if (index >= 0)
                {
                    SelectedFolderIndex = index;
                }
                else
                {
                    SelectedFolderIndex = -1;
                    SelectedCameraFolderName = string.Empty;
                }
            }
            else if (folders.Count > 0)
            {
                // default to first folder if none selected
                SelectedFolderIndex = 0;
                SelectedCameraFolderName = folders[0].FolderName;
            }
        }
        catch
        {
            AvailableFolders = new();
            SelectedFolderIndex = -1;
        }
    }

    [RelayCommand]
    private void RefreshCameraFoldersList()
    {
        RefreshCameraFolders();
    }

    [RelayCommand]
    private async Task CreateNewCameraFolder()
    {
        Status = "Creating folder...";
        var err = await Task.Run(() => _cameraService.CreateCameraFolder());
        if (err == EdsError.EDS_ERR_OK)
        {
            Status = "Folder created successfully";
            // refresh the folder list to show the newly created folder
            await Task.Delay(500); // Wait briefly for camera to finish creating folder
            RefreshCameraFolders();
        }
        else if (err == EdsError.EDS_ERR_INVALID_FN_CALL)
        {
            Status = "Folder creation not supported by this camera model.";
        }
        else
        {
            Status = $"Failed to create folder: {err} (0x{(uint)err:X8})";
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        // update settings object
        _settings.SavePath = SavePath;
        _settings.AutoDownload = AutoDownload;
        _settings.LiveViewFrameRate = LiveViewFrameRate;
        _settings.LiveViewDuringAutoFocus = LiveViewDuringAutoFocus;
        _settings.ConnectionTimeout = ConnectionTimeout;
        _settings.SaveDestination = IndexToSaveDestination(SaveDestinationIndex);
        _settings.FocusMediumSteps = FocusMediumSteps;
        _settings.FocusCoarseSteps = FocusCoarseSteps;
        _settings.SelectedCameraFolder = SelectedCameraFolderName;

        // persist to file
        _settingsService.Save(_settings);

        // apply immediately to the active runtime session (no reconnect required)
        _cameraService.SavePath = SavePath;
        _cameraService.SaveDestination = _settings.SaveDestination;
        _cameraService.LiveViewDuringAutoFocus = LiveViewDuringAutoFocus;
    }

    // get current settings (for other components to read)
    public AppSettings GetCurrentSettings() => _settings;

    private static int SaveDestinationToIndex(SaveDestination d) =>
        d switch
        {
            SaveDestination.Camera => 0,
            SaveDestination.Host => 1,
            SaveDestination.Both => 2,
            _ => 0,
        };

    private static SaveDestination IndexToSaveDestination(int index) =>
        index switch
        {
            1 => SaveDestination.Host,
            2 => SaveDestination.Both,
            _ => SaveDestination.Camera,
        };
}
