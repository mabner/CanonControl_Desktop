/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

using System;
using System.Runtime.InteropServices;
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
    private const double DefaultWindowHeight = 480;
    private const double DefaultLiveViewSurfaceWidth = 600;
    private const double DefaultLiveViewSurfaceHeight = 400;
    private const double RaspberryTaskbarHeight = 32;
    private const double RaspberryLiveViewSurfaceHeight = 368;

    private readonly CameraService _cameraService;
    private readonly SettingsService _settingsService;
    private System.Threading.CancellationTokenSource? _connectionPollingCts;

    // cached panel ViewModels — created once and reused so user-configured values persist during the session
    private RemoteCaptureViewModel? _remoteCaptureViewModel;
    private FocusStackViewModel? _focusStackViewModel;
    private ExposureBracketingViewModel? _exposureBracketingViewModel;
    private TimeLapseViewModel? _timeLapseViewModel;
    private SettingsViewModel? _settingsViewModel;

    public MainWindowViewModel(CameraService cameraService)
    {
        _cameraService = cameraService;
        _settingsService = new SettingsService();
        ApplyPlatformLayoutDefaults();

        // load and apply settings
        LoadSettings();
    }

    [ObservableProperty]
    private string _status = "Disconnected";

    [ObservableProperty]
    private double _windowHeight = DefaultWindowHeight;

    [ObservableProperty]
    private double _liveViewSurfaceWidth = DefaultLiveViewSurfaceWidth;

    [ObservableProperty]
    private double _liveViewSurfaceHeight = DefaultLiveViewSurfaceHeight;

    // navigation state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextCaptureLabel))]
    private NavigationContext _currentContext = NavigationContext.RemoteCapture;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextCaptureLabel))]
    private ViewModelBase? _currentSidePanelViewModel;

    // camera state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyPropertyChangedFor(nameof(CanDisconnect))]
    [NotifyCanExecuteChangedFor(nameof(ToggleLiveViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ContextCaptureCommand))]
    private bool _isCameraConnected;

    public bool CanConnect => !IsCameraConnected;

    public bool CanDisconnect => IsCameraConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _cameraName = string.Empty;

    [ObservableProperty]
    private int? _batteryPercentage;

    [ObservableProperty]
    private string _batteryStatus = "Battery Unknown";

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
    private CompositionAidMode _compositionAidMode = CompositionAidMode.None;

    [ObservableProperty]
    private bool _isCompositionMirrored = false;

    [ObservableProperty]
    private int _compositionRotation = 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(StartFocusNearCommand),
        nameof(StartFocusFarCommand),
        nameof(StopFocusCommand),
        nameof(StartAutoFocusCommand),
        nameof(StopAutoFocusCommand)
    )]
    private bool _isLiveViewActive;

    // settings
    private int _liveViewFrameRate = 30;

    // camera commands
    [RelayCommand]
    private async Task ConnectCamera()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("ConnectCamera command started");
            Status = "Connecting...";

            // load current settings to get connection timeout and capture destination
            var settings = _settingsService.Load();
            System.Diagnostics.Debug.WriteLine(
                $"Settings loaded, timeout: {settings.ConnectionTimeout}"
            );

            // apply runtime settings before connecting
            _cameraService.SavePath = settings.SavePath;
            _cameraService.SaveDestination = settings.SaveDestination;
            System.Diagnostics.Debug.WriteLine(
                $"Save path set to: {settings.SavePath}, destination: {settings.SaveDestination}"
            );

            var result = await _cameraService.ConnectAsync(settings.ConnectionTimeout);
            System.Diagnostics.Debug.WriteLine($"Connection result: {result}");

            IsCameraConnected = result;

            if (result)
            {
                CameraName = _cameraService.GetCameraName();
                Status = "Connected";
                System.Diagnostics.Debug.WriteLine($"Connected to camera: {CameraName}");

                // stop polling once connected
                StopConnectionPolling();

                // start battery monitoring for the connected camera
                StartBatteryPolling();

                // navigate to Remote Capture panel by default after connection
                NavigateToRemoteCapture();
            }
            else
            {
                if (_cameraService.LastConnectionAttemptFoundNoCamera)
                {
                    Status = "Waiting for camera...";
                    System.Diagnostics.Debug.WriteLine("Connection failed, starting polling");

                    // start polling to detect when camera becomes available
                    StartConnectionPolling();
                }
                else
                {
                    Status = _cameraService.LastConnectionError ?? "Connection failed";
                    System.Diagnostics.Debug.WriteLine($"Connection failed: {Status}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ConnectCamera exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Status = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DisconnectCamera()
    {
        // stop connection polling
        StopConnectionPolling();

        // stop polling if we're on RemoteCapture context
        if (CurrentSidePanelViewModel is RemoteCaptureViewModel rcvm)
        {
            rcvm.StopPolling();
        }

        _cameraService.Disconnect();

        IsCameraConnected = false;
        CameraName = string.Empty;
        Status = "Disconnected";
        BatteryPercentage = null;
        BatteryStatus = "Battery Unknown";
        StopBatteryPolling();
    }

    private System.Threading.CancellationTokenSource? _batteryPollingCts;

    private void StartBatteryPolling()
    {
        StopBatteryPolling();

        _batteryPollingCts = new System.Threading.CancellationTokenSource();
        var token = _batteryPollingCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await RefreshBatteryStateAsync();
                        await Task.Delay(10000, token);
                    }
                    catch (System.OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            BatteryPercentage = null;
                            BatteryStatus = "Battery Unknown";
                        });
                    }
                }
            },
            token
        );
    }

    private void StopBatteryPolling()
    {
        _batteryPollingCts?.Cancel();
        _batteryPollingCts?.Dispose();
        _batteryPollingCts = null;
    }

    private async Task RefreshBatteryStateAsync()
    {
        var batteryPercentage = await Task.Run(() => _cameraService.GetBatteryPercentage());
        var statusText = batteryPercentage.HasValue
            ? $"Battery {batteryPercentage.Value}%"
            : "Battery Unknown";

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            BatteryPercentage = batteryPercentage;
            BatteryStatus = statusText;
        });
    }

    private void StartConnectionPolling()
    {
        // stop any existing polling
        StopConnectionPolling();

        _connectionPollingCts = new System.Threading.CancellationTokenSource();
        var token = _connectionPollingCts.Token;

        Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(2000, token); // poll every 2 seconds

                        // only poll if not connected
                        if (!IsCameraConnected)
                        {
                            var settings = _settingsService.Load();

                            // apply runtime settings before connecting
                            _cameraService.SavePath = settings.SavePath;
                            _cameraService.SaveDestination = settings.SaveDestination;

                            var result = await _cameraService.ConnectAsync(
                                settings.ConnectionTimeout
                            );

                            if (result)
                            {
                                // update UI on main thread
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    IsCameraConnected = true;
                                    CameraName = _cameraService.GetCameraName();
                                    Status = "Connected";
                                    NavigateToRemoteCapture();
                                });

                                // stop polling once connected
                                break;
                            }
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // silently ignore errors during polling
                    }
                }
            },
            token
        );
    }

    private void StopConnectionPolling()
    {
        _connectionPollingCts?.Cancel();
        _connectionPollingCts?.Dispose();
        _connectionPollingCts = null;
    }

    // navigation commands
    [RelayCommand]
    private void NavigateToRemoteCapture()
    {
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.RemoteCapture;
        _remoteCaptureViewModel ??= new RemoteCaptureViewModel(_cameraService);
        CurrentSidePanelViewModel = _remoteCaptureViewModel;
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToFocusStack()
    {
        if (CurrentContext == NavigationContext.FocusStack && CurrentSidePanelViewModel != null)
            return;
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.FocusStack;
        _focusStackViewModel ??= new FocusStackViewModel(_cameraService);
        CurrentSidePanelViewModel = _focusStackViewModel;
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToExposureBracketing()
    {
        if (
            CurrentContext == NavigationContext.ExposureBracketing
            && CurrentSidePanelViewModel != null
        )
            return;
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.ExposureBracketing;
        _exposureBracketingViewModel ??= new ExposureBracketingViewModel(_cameraService);
        CurrentSidePanelViewModel = _exposureBracketingViewModel;
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToTimeLapse()
    {
        if (CurrentContext == NavigationContext.TimeLapse && CurrentSidePanelViewModel != null)
            return;
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.TimeLapse;
        _timeLapseViewModel ??= new TimeLapseViewModel(_cameraService);
        CurrentSidePanelViewModel = _timeLapseViewModel;
        SubscribeToCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        if (CurrentContext == NavigationContext.Settings && CurrentSidePanelViewModel != null)
            return;
        UnsubscribeFromCurrentViewModel();
        CurrentContext = NavigationContext.Settings;
        _settingsViewModel ??= new SettingsViewModel(_cameraService);
        CurrentSidePanelViewModel = _settingsViewModel;
        SubscribeToCurrentViewModel();
    }

    // subscribe to IsRunning property changes in feature ViewModels
    private void SubscribeToCurrentViewModel()
    {
        if (CurrentSidePanelViewModel is RemoteCaptureViewModel rcvm)
        {
            if (IsCameraConnected)
            {
                rcvm.StartPolling();
            }

            // subscribe to histogram mode changes
            rcvm.PropertyChanged += OnRemoteCapturePropertyChanged;
        }
        else if (CurrentSidePanelViewModel is FocusStackViewModel fsvm)
        {
            if (IsCameraConnected)
            {
                fsvm.UpdateCameraSettings();
            }
            fsvm.PropertyChanged += OnFeatureViewModelPropertyChanged;
        }
        else if (CurrentSidePanelViewModel is ExposureBracketingViewModel ebvm)
        {
            if (IsCameraConnected)
            {
                ebvm.UpdateCameraSettings();
            }
            ebvm.PropertyChanged += OnFeatureViewModelPropertyChanged;
        }
        else if (CurrentSidePanelViewModel is TimeLapseViewModel tlvm)
        {
            if (IsCameraConnected)
            {
                tlvm.UpdateCameraSettings();
            }
            tlvm.PropertyChanged += OnFeatureViewModelPropertyChanged;
        }
    }

    private void UnsubscribeFromCurrentViewModel()
    {
        if (CurrentSidePanelViewModel is RemoteCaptureViewModel rcvm)
        {
            rcvm.StopPolling();
            rcvm.PropertyChanged -= OnRemoteCapturePropertyChanged;
        }
        else if (CurrentSidePanelViewModel is FocusStackViewModel fsvm)
        {
            fsvm.PropertyChanged -= OnFeatureViewModelPropertyChanged;
        }
        else if (CurrentSidePanelViewModel is ExposureBracketingViewModel ebvm)
        {
            ebvm.PropertyChanged -= OnFeatureViewModelPropertyChanged;
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
            || e.PropertyName == nameof(ExposureBracketingViewModel.IsRunning)
            || e.PropertyName == nameof(TimeLapseViewModel.IsRunning)
        )
        {
            OnPropertyChanged(nameof(ContextCaptureLabel));
        }
    }

    private void OnRemoteCapturePropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e
    )
    {
        if (e.PropertyName == nameof(RemoteCaptureViewModel.HistogramMode))
        {
            if (sender is RemoteCaptureViewModel rcvm)
            {
                HistogramDisplayMode = rcvm.HistogramMode;
            }
        }
        else if (e.PropertyName == nameof(RemoteCaptureViewModel.CompositionMode))
        {
            if (sender is RemoteCaptureViewModel rcvm)
            {
                CompositionAidMode = rcvm.CompositionMode;
            }
        }
        else if (e.PropertyName == nameof(RemoteCaptureViewModel.IsCompositionMirrored))
        {
            if (sender is RemoteCaptureViewModel rcvm)
            {
                IsCompositionMirrored = rcvm.IsCompositionMirrored;
            }
        }
        else if (e.PropertyName == nameof(RemoteCaptureViewModel.CompositionRotation))
        {
            if (sender is RemoteCaptureViewModel rcvm)
            {
                CompositionRotation = rcvm.CompositionRotation;
            }
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        // apply settings to CameraService
        _cameraService.SavePath = settings.SavePath;
        _cameraService.SaveDestination = settings.SaveDestination;
        _cameraService.LiveViewDuringAutoFocus = settings.LiveViewDuringAutoFocus;
        _cameraService.FocusMediumSteps = settings.FocusMediumSteps;
        _cameraService.FocusCoarseSteps = settings.FocusCoarseSteps;

        // store live view frame rate for use when starting live view
        _liveViewFrameRate = settings.LiveViewFrameRate;
    }

    private void ApplyPlatformLayoutDefaults()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        WindowHeight = DefaultWindowHeight - RaspberryTaskbarHeight;
        LiveViewSurfaceHeight = RaspberryLiveViewSurfaceHeight;
        LiveViewSurfaceWidth = LiveViewSurfaceHeight * 1.5;
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
                // reload settings to get the latest frame rate
                var settings = _settingsService.Load();
                _liveViewFrameRate = settings.LiveViewFrameRate;

                // start the live view task (it runs in background)
                var liveViewTask = _cameraService.StartLiveViewAsync(
                    frameData =>
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
                    },
                    _liveViewFrameRate
                );

                IsLiveViewActive = true;

                // start histogram updates
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
                        await Task.Delay(500, token); // update every 500ms
                    }
                    catch (System.OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // silently ignore errors
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
                NavigationContext.FocusStack => IsStackRunning ? "Stop\nStack" : "Start\nStack",
                NavigationContext.ExposureBracketing => IsBracketingRunning
                    ? "Stop\nBracketing"
                    : "Start\nBracketing",
                NavigationContext.TimeLapse => IsLapseRunning ? "Stop\nLapse" : "Start\nLapse",
                NavigationContext.Settings => "Capture",
                _ => "Capture",
            };
        }
    }

    // helper properties to track running state of feature ViewModels
    private bool IsStackRunning =>
        CurrentSidePanelViewModel is FocusStackViewModel fsvm && fsvm.IsRunning;
    private bool IsBracketingRunning =>
        CurrentSidePanelViewModel is ExposureBracketingViewModel ebvm && ebvm.IsRunning;
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

            case NavigationContext.ExposureBracketing:
                if (CurrentSidePanelViewModel is ExposureBracketingViewModel ebvm)
                {
                    if (ebvm.IsRunning)
                    {
                        ebvm.StopBracketing();
                    }
                    else
                    {
                        await ebvm.StartBracketing();
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
