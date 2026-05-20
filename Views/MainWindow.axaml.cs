using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CanonControl.ViewModels;

namespace CanonControl.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // wire up pointer events for focus buttons (press-and-hold functionality)
        // handledEventsToo=true ensures we receive events even though buttons handle them internally
        FocusNearButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnFocusNearPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusNearButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusNearButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        FocusFarButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnFocusFarPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusFarButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusFarButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        AutoFocusButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnAutoFocusPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        AutoFocusButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnAutoFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        AutoFocusButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnAutoFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        FocusNearMediumButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnFocusNearMediumPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusNearMediumButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusNearMediumButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        FocusNearCoarseButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnFocusNearCoarsePressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusNearCoarseButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusNearCoarseButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        FocusFarMediumButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnFocusFarMediumPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusFarMediumButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusFarMediumButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        FocusFarCoarseButton.AddHandler(
            InputElement.PointerPressedEvent,
            OnFocusFarCoarsePressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusFarCoarseButton.AddHandler(
            InputElement.PointerReleasedEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );
        FocusFarCoarseButton.AddHandler(
            InputElement.PointerCaptureLostEvent,
            OnFocusReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true
        );

        // subscribe to DataContext changes to update focus overlay
        this.DataContextChanged += (_, __) => SubscribeVmPropertyChanges();
        SubscribeVmPropertyChanges();
    }

    private void SubscribeVmPropertyChanges()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged -= Vm_PropertyChanged;
            vm.PropertyChanged += Vm_PropertyChanged;
            UpdateFocusRect(vm);
        }
    }

    private void Vm_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e
    )
    {
        if (sender is MainWindowViewModel vm)
        {
            if (
                e.PropertyName == nameof(vm.FocusPointX)
                || e.PropertyName == nameof(vm.FocusPointY)
                || e.PropertyName == nameof(vm.FocusLocked)
                || e.PropertyName == nameof(vm.LiveViewSurfaceWidth)
                || e.PropertyName == nameof(vm.LiveViewSurfaceHeight)
            )
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateFocusRect(vm));
            }
        }
    }

    private void UpdateFocusRect(MainWindowViewModel vm)
    {
        try
        {
            if (FocusRect == null || FocusOverlayCanvas == null)
                return;

            double canvasWidth = vm.LiveViewSurfaceWidth;
            double canvasHeight = vm.LiveViewSurfaceHeight;

            double rectW = FocusRect.Width;
            double rectH = FocusRect.Height;

            double left = vm.FocusPointX * canvasWidth - rectW / 2.0;
            double top = vm.FocusPointY * canvasHeight - rectH / 2.0;

            Canvas.SetLeft(FocusRect, Math.Max(0, left));
            Canvas.SetTop(FocusRect, Math.Max(0, top));

            FocusRect.Stroke = vm.FocusLocked
                ? Avalonia.Media.Brushes.LimeGreen
                : Avalonia.Media.Brushes.White;
        }
        catch
        {
            // ignore drawing errors
        }
    }

    private void OnFocusNearPressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartFocusNearCommand);
    }

    private void OnFocusFarPressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartFocusFarCommand);
    }

    private void OnFocusReleased(object? sender, PointerEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StopFocusCommand);
    }

    private void OnAutoFocusPressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartAutoFocusCommand);
    }

    private void OnAutoFocusReleased(object? sender, PointerEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StopAutoFocusCommand);
    }

    private void OnFocusNearMediumPressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartFocusNearMediumCommand);
    }

    private void OnFocusNearCoarsePressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartFocusNearCoarseCommand);
    }

    private void OnFocusFarMediumPressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartFocusFarMediumCommand);
    }

    private void OnFocusFarCoarsePressed(object? sender, PointerPressedEventArgs e)
    {
        ExecuteVmCommand(vm => vm.StartFocusFarCoarseCommand);
    }

    private void ExecuteVmCommand(Func<MainWindowViewModel, ICommand> getCommand)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var command = getCommand(vm);

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnLiveViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.IsLiveViewActive)
        {
            var grid = sender as Grid;
            if (grid == null)
                return;

            var point = e.GetPosition(grid);
            double xNormalized = point.X / grid.Bounds.Width;
            double yNormalized = point.Y / grid.Bounds.Height;

            vm.SetFocusPoint(xNormalized, yNormalized);
        }
    }
}
