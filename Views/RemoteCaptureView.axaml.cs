using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CanonControl.ViewModels;

namespace CanonControl.Views;

public partial class RemoteCaptureView : UserControl
{
    public RemoteCaptureView()
    {
        InitializeComponent();
    }

    private void OnDelaySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (
            sender is ComboBox comboBox
            && comboBox.SelectedItem is ComboBoxItem item
            && item.Tag is string tagValue
            && DataContext is RemoteCaptureViewModel viewModel
        )
        {
            if (int.TryParse(tagValue, out int delay))
            {
                viewModel.DelaySeconds = delay;
            }
        }
    }
}
