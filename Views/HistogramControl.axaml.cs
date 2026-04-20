/*
* CanonControl
* Copyright (c) [2026] [Marcos Leite]
*
* This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-sa/4.0/
* or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
*/

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CanonControl.Models;

namespace CanonControl.Views;

public partial class HistogramControl : UserControl
{
    public static readonly StyledProperty<HistogramData?> HistogramProperty =
        AvaloniaProperty.Register<HistogramControl, HistogramData?>(nameof(Histogram));

    public static readonly StyledProperty<HistogramDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<HistogramControl, HistogramDisplayMode>(
            nameof(DisplayMode),
            HistogramDisplayMode.Luminance
        );

    public HistogramData? Histogram
    {
        get => GetValue(HistogramProperty);
        set => SetValue(HistogramProperty, value);
    }

    public HistogramDisplayMode DisplayMode
    {
        get => GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    public HistogramControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HistogramProperty || change.Property == DisplayModeProperty)
        {
            DrawHistogram();
        }
    }

    private void DrawHistogram()
    {
        HistogramCanvas.Children.Clear();

        if (Histogram == null || !Histogram.IsVisible || DisplayMode == HistogramDisplayMode.None)
            return;

        var width = Width;
        var height = Height;

        if (double.IsNaN(width) || double.IsNaN(height))
            return;

        // Find max value for scaling
        uint maxValue = 0;

        if (DisplayMode == HistogramDisplayMode.Luminance)
        {
            for (int i = 0; i < 256; i++)
            {
                maxValue = Math.Max(maxValue, Histogram.Luminance[i]);
            }
        }
        else // RGB mode
        {
            for (int i = 0; i < 256; i++)
            {
                maxValue = Math.Max(
                    maxValue,
                    Math.Max(Histogram.Red[i], Math.Max(Histogram.Green[i], Histogram.Blue[i]))
                );
            }
        }

        if (maxValue == 0)
            return;

        if (DisplayMode == HistogramDisplayMode.Luminance)
        {
            // Draw luminance histogram (white/gray)
            var luminanceBrush = new SolidColorBrush(Colors.White, 0.7);
            DrawHistogramChannel(Histogram.Luminance, maxValue, width, height, luminanceBrush);
        }
        else // RGB mode
        {
            // Draw RGB histograms (semi-transparent)
            var redBrush = new SolidColorBrush(Colors.Red, 0.6);
            DrawHistogramChannel(Histogram.Red, maxValue, width, height, redBrush);

            var greenBrush = new SolidColorBrush(Colors.Green, 0.6);
            DrawHistogramChannel(Histogram.Green, maxValue, width, height, greenBrush);

            var blueBrush = new SolidColorBrush(Colors.Blue, 0.6);
            DrawHistogramChannel(Histogram.Blue, maxValue, width, height, blueBrush);
        }
    }

    private void DrawHistogramChannel(
        uint[] data,
        uint maxValue,
        double width,
        double height,
        IBrush brush
    )
    {
        var barWidth = width / 256.0;

        for (int i = 0; i < 256; i++)
        {
            if (data[i] == 0)
                continue;

            var barHeight = (data[i] / (double)maxValue) * height;
            var x = i * barWidth;
            var y = height - barHeight;

            var rect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = Math.Max(1, barWidth),
                Height = barHeight,
                Fill = brush,
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);

            HistogramCanvas.Children.Add(rect);
        }
    }
}
