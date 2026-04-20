/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CanonControl.Models;
using CanonControl.Services;
using CanonControl.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanonControl.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CameraService _cameraService;
    private readonly SettingsService _settingsService;

    public MainWindowViewModel()
    {
        _cameraService = new CameraService();
        _settingsService = new SettingsService();

        // load and apply settings
        LoadSettings();
    }

    [ObservableProperty]
    private string _status = "Disconnected";

    // navigation state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextCaptureLabel))]
    private NavigationContext _currentContext = NavigationContext.RemoteCapture;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextCaptureLabel))]
    private ViewModelBase? _currentSidePanelViewModel;

    // camera state
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleLiveViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ContextCaptureCommand))]
    private bool _isCameraConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _cameraName = string.Empty;

    // computed property for window title
    public string WindowTitle =>
        string.IsNullOrEmpty(CameraName) ? "CanonControl" : $"CanonControl - {CameraName}";

    // live View State
    [ObservableProperty]
    private Bitmap? _liveImage;

    [ObservableProperty]
    private HistogramData? _histogramData;

    [ObservableProperty]
    private HistogramDisplayMode _histogramDisplayMode = HistogramDisplayMode.None;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(StartFocusNearCommand),
        nameof(StartFocusFarCommand),
        nameof(StopFocusCommand),
        nameof(StartAutoFocusCommand),
        nameof(StopAutoFocusCommand)
    )]
    private bool _isLiveViewActive;

    // camera commands
    [RelayCommand]
    private async Task ConnectCamera()
    {
        Status = "Connecting...";
        // load current settings to get connection timeout
        var settings = _settingsService.Load();

        var result = await _cameraService.ConnectAsync(settings.ConnectionTimeout);

        IsCameraConnected = result;

        if (result)
        {
            CameraName = _cameraService.GetCameraName();
            Status = "Connected";
        }
        else
        {
            Status = "Failed to connect";
            await ShowErrorDialogAsync(
                "Camera Connection Failed",
                "No Canon camera detected. Please connect a camera and try again."
            );
        }
    }

    [RelayCommand]
    private void DisconnectCamera()
    {
        _cameraService.Disconnect();

        IsCameraConnected = false;
        CameraName = string.Empty;
        Status = "Disconnected";
    }

    // navigation commands
    [RelayCommand]
    private void NavigateToRemoteCapture()
    {
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.RemoteCapture;
        CurrentSidePanelViewModel = new RemoteCaptureViewModel(_cameraService);
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToFocusStack()
    {
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.FocusStack;
        CurrentSidePanelViewModel = new FocusStackViewModel(_cameraService);
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToTimeLapse()
    {
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.TimeLapse;
        CurrentSidePanelViewModel = new TimeLapseViewModel(_cameraService);
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.Settings;
        CurrentSidePanelViewModel = new SettingsViewModel();
        SubscribeToCurrentViewModel();
    }

    // subscribe to IsRunning property changes in feature ViewModels
    private void SubscribeToCurrentViewModel()
    {
        if (CurrentSidePanelViewModel is FocusStackViewModel fsvm)
        {
            fsvm.PropertyChanged += OnFeatureViewModelPropertyChanged;
        }
        else if (CurrentSidePanelViewModel is TimeLapseViewModel tlvm)
        {
            tlvm.PropertyChanged += OnFeatureViewModelPropertyChanged;
        }
    }

    private void UnsubscribeFromCurrentViewModel()
    {
        if (CurrentSidePanelViewModel is FocusStackViewModel fsvm)
        {
            fsvm.PropertyChanged -= OnFeatureViewModelPropertyChanged;
        }
        else if (CurrentSidePanelViewModel is TimeLapseViewModel tlvm)
        {
            tlvm.PropertyChanged -= OnFeatureViewModelPropertyChanged;
        }
    }

    private void OnFeatureViewModelPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e
    )
    {
        if (
            e.PropertyName == nameof(FocusStackViewModel.IsRunning)
            || e.PropertyName == nameof(TimeLapseViewModel.IsRunning)
        )
        {
            OnPropertyChanged(nameof(ContextCaptureLabel));
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        // apply settings to CameraService
        _cameraService.LiveViewDuringAutoFocus = settings.LiveViewDuringAutoFocus;

        // TODO: apply other settings (LiveViewFrameRate, etc.) when those features are implemented
    }

    // control panel commands
    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private async Task ToggleLiveView()
    {
        if (!IsLiveViewActive)
        {
            // start live view
            try
            {
                // start the live view task (it runs in background)
                var liveViewTask = _cameraService.StartLiveViewAsync(frameData =>
                {
                    // convert byte[] to Bitmap and update LiveImage on UI thread
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            using var stream = new System.IO.MemoryStream(frameData);
                            LiveImage = new Bitmap(stream);
                        }
                        catch
                        {
                            // silently handle bitmap creation errors
                        }
                    });
                });

                IsLiveViewActive = true;

                // Start histogram updates
                StartHistogramUpdates();

                // explicitly notify focus commands to re-evaluate CanExecute
                StartFocusNearCommand.NotifyCanExecuteChanged();
                StartFocusFarCommand.NotifyCanExecuteChanged();
                StopFocusCommand.NotifyCanExecuteChanged();
                StartAutoFocusCommand.NotifyCanExecuteChanged();
                StopAutoFocusCommand.NotifyCanExecuteChanged();

                // don't await the live view task - it runs continuously until stopped
            }
            catch
            {
                // silently handle errors - keep toggle in current state
                IsLiveViewActive = false;
            }
        }
        else
        {
            // stop live view
            try
            {
                StopHistogramUpdates();
                _cameraService.StopLiveView();
                IsLiveViewActive = false;
                LiveImage = null;
                HistogramData = null;

                // explicitly notify focus commands to re-evaluate CanExecute
                StartFocusNearCommand.NotifyCanExecuteChanged();
                StartFocusFarCommand.NotifyCanExecuteChanged();
                StopFocusCommand.NotifyCanExecuteChanged();
                StartAutoFocusCommand.NotifyCanExecuteChanged();
                StopAutoFocusCommand.NotifyCanExecuteChanged();
            }
            catch
            {
                // silently handle errors - keep toggle in current state
            }
        }
    }

    private System.Threading.CancellationTokenSource? _histogramCts;

    private void StartHistogramUpdates()
    {
        _histogramCts = new System.Threading.CancellationTokenSource();
        var token = _histogramCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var histogram = _cameraService.GetHistogramData();
                        if (histogram != null)
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                HistogramData = histogram;
                            });
                        }
                        await Task.Delay(500, token); // Update every 500ms
                    }
                    catch (System.OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Silently ignore errors
                    }
                }
            },
            token
        );
    }

    private void StopHistogramUpdates()
    {
        _histogramCts?.Cancel();
        _histogramCts?.Dispose();
        _histogramCts = null;
    }

    private bool CanExecuteFocusCommands() => IsLiveViewActive;

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StartFocusNear()
    {
        _cameraService.StartFocusNear();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StartFocusFar()
    {
        _cameraService.StartFocusFar();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StopFocus()
    {
        _cameraService.StopFocus();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StartAutoFocus()
    {
        _cameraService.StartAutoFocus();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteFocusCommands))]
    private void StopAutoFocus()
    {
        _cameraService.StopAutoFocus();
    }

    // computed property for context-aware capture button label
    public string ContextCaptureLabel
    {
        get
        {
            return CurrentContext switch
            {
                NavigationContext.RemoteCapture => "Capture",
                NavigationContext.FocusStack => IsStackRunning ? "Stop Stack" : "Start Stack",
                NavigationContext.TimeLapse => IsLapseRunning ? "Stop Lapse" : "Start Lapse",
                NavigationContext.Settings => "Capture",
                _ => "Capture",
            };
        }
    }

    // helper properties to track running state of feature ViewModels
    private bool IsStackRunning =>
        CurrentSidePanelViewModel is FocusStackViewModel fsvm && fsvm.IsRunning;
    private bool IsLapseRunning =>
        CurrentSidePanelViewModel is TimeLapseViewModel tlvm && tlvm.IsRunning;

    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private async Task ContextCapture()
    {
        // delegate to appropriate method based on CurrentContext
        switch (CurrentContext)
        {
            case NavigationContext.RemoteCapture:
                if (CurrentSidePanelViewModel is RemoteCaptureViewModel rcvm)
                {
                    await rcvm.TakeSinglePicture();
                }
                break;

            case NavigationContext.FocusStack:
                if (CurrentSidePanelViewModel is FocusStackViewModel fsvm)
                {
                    if (fsvm.IsRunning)
                    {
                        fsvm.StopStack();
                    }
                    else
                    {
                        await fsvm.StartStack();
                    }
                }
                break;

            case NavigationContext.TimeLapse:
                if (CurrentSidePanelViewModel is TimeLapseViewModel tlvm)
                {
                    if (tlvm.IsRunning)
                    {
                        tlvm.StopLapse();
                    }
                    else
                    {
                        await tlvm.StartLapse();
                    }
                }
                break;

            case NavigationContext.Settings:
                // fallback: do nothing or take a single picture
                break;
        }
    }

    // legacy window-opening commands (to be deprecated)
    [RelayCommand]
    private void OpenLiveView()
    {
        // share conection in the same session
        var window = new LiveViewWindow(new LiveViewViewModel(_cameraService));

        window.Show();
    }

    [RelayCommand]
    private void OpenFocusStack()
    {
        var window = new FocusStackView { DataContext = new FocusStackViewModel(_cameraService) };

        //window.Show();
    }

    [RelayCommand]
    private void OpenTimeLapse()
    {
        var window = new TimeLapseView { DataContext = new TimeLapseViewModel(_cameraService) };

        //window.Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new SettingsWindow { DataContext = new SettingsViewModel() };

        window.Show();
    }

    // helper method to show error dialogs
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        if (
            Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                var dialog = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 200,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Spacing = 20,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            },
                            new Button
                            {
                                Content = "OK",
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                MinWidth = 100,
                                MinHeight = 44,
                            },
                        },
                    },
                };

                // wire up the OK button to close the dialog
                if (dialog.Content is StackPanel panel && panel.Children[1] is Button okButton)
                {
                    okButton.Click += (s, e) => dialog.Close();
                }

                await dialog.ShowDialog(mainWindow);
            }
        }
    }
}
