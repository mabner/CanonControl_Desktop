using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CanonControl.ViewModels;

namespace CanonControl.Views;

public partial class LiveViewWindow : Window
{
    public LiveViewWindow()
    {
        InitializeComponent();
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnImageClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && DataContext is LiveViewViewModel vm)
        {
            var point = e.GetPosition(control);
            vm.FocusAtPoint(point.X, point.Y);
        }
    }
}
