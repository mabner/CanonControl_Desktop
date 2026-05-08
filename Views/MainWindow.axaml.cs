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
}
