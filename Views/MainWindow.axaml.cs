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
