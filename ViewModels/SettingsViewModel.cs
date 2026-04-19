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
    private readonly AppSettings _settings;

    public SettingsViewModel()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        // initialize properties from loaded settings
        _savePath = _settings.SavePath;
        _autoDownload = _settings.AutoDownload;
        _liveViewFrameRate = _settings.LiveViewFrameRate;
        _liveViewDuringAutoFocus = _settings.LiveViewDuringAutoFocus;
    }

    [ObservableProperty]
    private string _savePath = string.Empty;

    [ObservableProperty]
    private bool _autoDownload = true;

    [ObservableProperty]
    private int _liveViewFrameRate = 30;

    [ObservableProperty]
    private bool _liveViewDuringAutoFocus = true;

    // display the settings file path for user reference
    public string SettingsFilePath => _settingsService.GetSettingsPath();

    // available frame rate options
    public List<int> FrameRateOptions { get; } = new() { 15, 20, 30 };

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

        // persist to file
        _settingsService.Save(_settings);
    }

    // get current settings (for other components to read)
    public AppSettings GetCurrentSettings() => _settings;
}
