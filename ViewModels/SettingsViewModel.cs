using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        // Keep active runtime session aligned with persisted settings when opening Settings panel.
        _cameraService.SavePath = _savePath;
        _cameraService.SaveDestination = _settings.SaveDestination;
        _cameraService.LiveViewDuringAutoFocus = _liveViewDuringAutoFocus;
    }

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
