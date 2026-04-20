using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CanonControl.Models;

namespace CanonControl.Views;

public partial class CompositionAidControl : UserControl
{
    public static readonly StyledProperty<CompositionAidMode> CompositionModeProperty =
        AvaloniaProperty.Register<CompositionAidControl, CompositionAidMode>(
            nameof(CompositionMode),
            defaultValue: CompositionAidMode.None
        );

    public CompositionAidMode CompositionMode
    {
        get => GetValue(CompositionModeProperty);
        set => SetValue(CompositionModeProperty, value);
    }

    public static readonly StyledProperty<bool> IsMirroredProperty = AvaloniaProperty.Register<
        CompositionAidControl,
        bool
    >(nameof(IsMirrored), defaultValue: false);

    public bool IsMirrored
    {
        get => GetValue(IsMirroredProperty);
        set => SetValue(IsMirroredProperty, value);
    }

    public static readonly StyledProperty<int> RotationProperty = AvaloniaProperty.Register<
        CompositionAidControl,
        int
    >(nameof(Rotation), defaultValue: 0);

    public int Rotation
    {
        get => GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    private readonly Pen _linePen;

    public CompositionAidControl()
    {
        InitializeComponent();

        // semi-transparent white lines (similar to histogram overlay style)
        _linePen = new Pen(new SolidColorBrush(Colors.White, 0.7), 1.5);

        // redraw when composition mode changes
        CompositionModeProperty.Changed.AddClassHandler<CompositionAidControl>(
            (control, e) =>
            {
                control.InvalidateVisual();
            }
        );

        // redraw when mirror or rotation changes
        IsMirroredProperty.Changed.AddClassHandler<CompositionAidControl>(
            (control, e) =>
            {
                control.InvalidateVisual();
            }
        );

        RotationProperty.Changed.AddClassHandler<CompositionAidControl>(
            (control, e) =>
            {
                control.InvalidateVisual();
            }
        );

        // redraw when size changes
        SizeChanged += (s, e) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (CompositionMode == CompositionAidMode.None || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var width = Bounds.Width;
        var height = Bounds.Height;

        switch (CompositionMode)
        {
            case CompositionAidMode.RuleOfThirds:
                DrawRuleOfThirds(context, width, height);
                break;

            case CompositionAidMode.GoldenRatio:
                DrawGoldenRatio(context, width, height);
                break;

            case CompositionAidMode.GoldenTriangle:
                DrawGoldenTriangle(context, width, height);
                break;

            case CompositionAidMode.CenterCross:
                DrawCenterCross(context, width, height);
                break;

            case CompositionAidMode.DiagonalLines:
                DrawDiagonalLines(context, width, height);
                break;

            case CompositionAidMode.Grid:
                DrawGrid(context, width, height);
                break;
        }
    }

    private void DrawRuleOfThirds(DrawingContext context, double width, double height)
    {
        // 2 horizontal lines
        double h1 = height / 3.0;
        double h2 = 2.0 * height / 3.0;
        context.DrawLine(_linePen, new Point(0, h1), new Point(width, h1));
        context.DrawLine(_linePen, new Point(0, h2), new Point(width, h2));

        // 2 vertical lines
        double v1 = width / 3.0;
        double v2 = 2.0 * width / 3.0;
        context.DrawLine(_linePen, new Point(v1, 0), new Point(v1, height));
        context.DrawLine(_linePen, new Point(v2, 0), new Point(v2, height));
    }

    private void DrawGoldenRatio(DrawingContext context, double width, double height)
    {
        // determine if we need to swap dimensions based on rotation
        int normalizedRotation = ((Rotation % 360) + 360) % 360;
        bool isVertical = normalizedRotation == 90 || normalizedRotation == 270;

        // apply mirror transformation only (rotation is handled by redrawing)
        if (IsMirrored)
        {
            using (context.PushTransform(GetMirrorMatrix(width, height)))
            {
                DrawFibonacciSquares(context, width, height, isVertical);
            }
        }
        else
        {
            DrawFibonacciSquares(context, width, height, isVertical);
        }
    }

    private Matrix GetMirrorMatrix(double width, double height)
    {
        var matrix = Matrix.Identity;
        double centerX = width / 2.0;

        // apply mirror (horizontal flip)
        matrix = Matrix.CreateTranslation(-centerX, 0);
        matrix *= Matrix.CreateScale(-1, 1);
        matrix *= Matrix.CreateTranslation(centerX, 0);

        return matrix;
    }

    private void DrawFibonacciSquares(
        DrawingContext context,
        double width,
        double height,
        bool isVertical
    )
    {
        // draw the Fibonacci rectangles/squares that form the golden ratio composition
        // each square is placed next to the remaining rectangle, creating the classic pattern

        // Fibonacci sequence - note the two 1s at the start
        double[] fib = { 1, 1, 2, 3, 5, 8, 13 };

        // the golden rectangle base dimensions (horizontal: 21x13, vertical: 13x21)
        double totalWidth,
            totalHeight;
        int rotationOffset;

        if (isVertical)
        {
            // vertical orientation: swap dimensions and adjust starting direction
            totalWidth = fib[^1]; // 13
            totalHeight = fib[^1] + fib[^2]; // 21

            // determine rotation offset for drawing direction
            int normalizedRotation = ((Rotation % 360) + 360) % 360;
            rotationOffset = normalizedRotation == 90 ? 1 : 3; // 90° starts top, 270° starts bottom
        }
        else
        {
            // horizontal orientation
            totalWidth = fib[^1] + fib[^2]; // 21
            totalHeight = fib[^1]; // 13

            // determine rotation offset for drawing direction
            int normalizedRotation = ((Rotation % 360) + 360) % 360;
            rotationOffset = normalizedRotation == 180 ? 2 : 0; // 180° starts right, 0° starts left
        }

        // scale to fill the entire frame
        double scaleX = width / totalWidth;
        double scaleY = height / totalHeight;
        double scale = Math.Min(scaleX, scaleY);

        // center the composition if aspect ratios don't match exactly
        double offsetX = (width - totalWidth * scale) / 2;
        double offsetY = (height - totalHeight * scale) / 2;

        // draw the outer golden rectangle
        context.DrawRectangle(
            null,
            _linePen,
            new Rect(offsetX, offsetY, totalWidth * scale, totalHeight * scale)
        );

        // track the remaining rectangle as we subdivide
        double rectX = offsetX;
        double rectY = offsetY;
        double rectW = totalWidth * scale;
        double rectH = totalHeight * scale;

        // draw Fibonacci squares, each time subdividing the remaining rectangle
        for (int i = fib.Length - 1; i >= 1; i--)
        {
            double squareSize = fib[i] * scale;
            int direction = ((fib.Length - 1 - i) + rotationOffset) % 4;

            switch (direction)
            {
                case 0: // square on left, remaining rectangle on right
                    context.DrawRectangle(
                        null,
                        _linePen,
                        new Rect(rectX, rectY, squareSize, squareSize)
                    );
                    rectX += squareSize;
                    rectW -= squareSize;
                    break;

                case 1: // square on top, remaining rectangle below
                    context.DrawRectangle(
                        null,
                        _linePen,
                        new Rect(rectX, rectY, squareSize, squareSize)
                    );
                    rectY += squareSize;
                    rectH -= squareSize;
                    break;

                case 2: // square on right, remaining rectangle on left
                    double rightX = rectX + rectW - squareSize;
                    context.DrawRectangle(
                        null,
                        _linePen,
                        new Rect(rightX, rectY, squareSize, squareSize)
                    );
                    rectW -= squareSize;
                    break;

                case 3: // square on bottom, remaining rectangle on top
                    double bottomY = rectY + rectH - squareSize;
                    context.DrawRectangle(
                        null,
                        _linePen,
                        new Rect(rectX, bottomY, squareSize, squareSize)
                    );
                    rectH -= squareSize;
                    break;
            }
        }
    }

    private void DrawGoldenTriangle(DrawingContext context, double width, double height)
    {
        // determine if we need to swap dimensions based on rotation
        int normalizedRotation = ((Rotation % 360) + 360) % 360;
        bool isVertical = normalizedRotation == 90 || normalizedRotation == 270;

        // apply mirror transformation only (rotation is handled by redrawing)
        if (IsMirrored)
        {
            using (context.PushTransform(GetMirrorMatrix(width, height)))
            {
                DrawGoldenTriangleLines(context, width, height, isVertical, normalizedRotation);
            }
        }
        else
        {
            DrawGoldenTriangleLines(context, width, height, isVertical, normalizedRotation);
        }
    }

    private void DrawGoldenTriangleLines(
        DrawingContext context,
        double width,
        double height,
        bool isVertical,
        int rotation
    )
    {
        Point corner1,
            corner2,
            corner3,
            corner4;

        if (rotation == 0)
        {
            // main diagonal: bottom-left to top-right
            corner1 = new Point(0, height); // bottom-left (start of main diagonal)
            corner2 = new Point(width, 0); // top-right (end of main diagonal)
            corner3 = new Point(0, 0); // top-left
            corner4 = new Point(width, height); // bottom-right
        }
        else if (rotation == 90)
        {
            // main diagonal: bottom-right to top-left
            corner1 = new Point(width, height); // bottom-right (start of main diagonal)
            corner2 = new Point(0, 0); // top-left (end of main diagonal)
            corner3 = new Point(width, 0); // top-right
            corner4 = new Point(0, height); // bottom-left
        }
        else if (rotation == 180)
        {
            // main diagonal: top-right to bottom-left
            corner1 = new Point(width, 0); // top-right (start of main diagonal)
            corner2 = new Point(0, height); // bottom-left (end of main diagonal)
            corner3 = new Point(width, height); // bottom-right
            corner4 = new Point(0, 0); // top-left
        }
        else // 270
        {
            // main diagonal: top-left to bottom-right
            corner1 = new Point(0, 0); // top-left (start of main diagonal)
            corner2 = new Point(width, height); // bottom-right (end of main diagonal)
            corner3 = new Point(0, height); // bottom-left
            corner4 = new Point(width, 0); // top-right
        }

        // draw main diagonal
        context.DrawLine(_linePen, corner1, corner2);

        // calculate perpendicular lines from the other two corners
        // These lines should be perpendicular to the main diagonal

        // for corner3: draw perpendicular to main diagonal
        Point perp3 = CalculatePerpendicularIntersection(corner3, corner1, corner2);
        context.DrawLine(_linePen, corner3, perp3);

        // for corner4: draw perpendicular to main diagonal
        Point perp4 = CalculatePerpendicularIntersection(corner4, corner1, corner2);
        context.DrawLine(_linePen, corner4, perp4);
    }

    private Point CalculatePerpendicularIntersection(Point from, Point lineStart, Point lineEnd)
    {
        // calculate the point on line (lineStart to lineEnd) that forms a perpendicular with 'from'

        // vector along the line
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;

        // vector from lineStart to 'from'
        double fx = from.X - lineStart.X;
        double fy = from.Y - lineStart.Y;

        // project 'from' onto the line
        double lineLengthSquared = dx * dx + dy * dy;
        double t = (fx * dx + fy * dy) / lineLengthSquared;

        // calculate intersection point
        double intersectX = lineStart.X + t * dx;
        double intersectY = lineStart.Y + t * dy;

        return new Point(intersectX, intersectY);
    }

    private void DrawCenterCross(DrawingContext context, double width, double height)
    {
        // simple crosshair at center
        double centerX = width / 2.0;
        double centerY = height / 2.0;

        // horizontal line
        context.DrawLine(_linePen, new Point(0, centerY), new Point(width, centerY));

        // vertical line
        context.DrawLine(_linePen, new Point(centerX, 0), new Point(centerX, height));
    }

    private void DrawDiagonalLines(DrawingContext context, double width, double height)
    {
        // lines from corners to opposite corners
        context.DrawLine(_linePen, new Point(0, 0), new Point(width, height));
        context.DrawLine(_linePen, new Point(width, 0), new Point(0, height));
    }

    private void DrawGrid(DrawingContext context, double width, double height)
    {
        // 5x5 grid for precise alignment
        int gridSize = 5;

        // horizontal lines
        for (int i = 1; i < gridSize; i++)
        {
            double y = i * height / gridSize;
            context.DrawLine(_linePen, new Point(0, y), new Point(width, y));
        }

        // vertical lines
        for (int i = 1; i < gridSize; i++)
        {
            double x = i * width / gridSize;
            context.DrawLine(_linePen, new Point(x, 0), new Point(x, height));
        }
    }
}
