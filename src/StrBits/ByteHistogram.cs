using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StrBits;

public sealed class ByteHistogram : Control
{
    public static readonly StyledProperty<int[]?> CountsProperty =
        AvaloniaProperty.Register<ByteHistogram, int[]?>(nameof(Counts));

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<ByteHistogram, IBrush?>(nameof(BarBrush));
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<ByteHistogram, IBrush?>(nameof(GridBrush));

    static ByteHistogram() => AffectsRender<ByteHistogram>(CountsProperty, BarBrushProperty, GridBrushProperty);
    public int[]? Counts { get => GetValue(CountsProperty); set => SetValue(CountsProperty, value); }
    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var counts = Counts;
        var height = Bounds.Height;
        var width = Bounds.Width;
        var grid = new Pen(GridBrush, 1);
        for (var i = 0; i <= 2; i++)
            context.DrawLine(grid, new Point(0, i * (height - 1) / 2), new Point(width, i * (height - 1) / 2));
        if (counts is null || counts.Length == 0 || counts.Max() == 0 || BarBrush is null) return;
        var max = counts.Max();
        var step = width / counts.Length;
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] == 0) continue;
            var barHeight = (height - 2) * counts[i] / max;
            context.FillRectangle(BarBrush, new Rect(i * step, height - barHeight - 1, Math.Max(1, step - 0.5), barHeight));
        }
    }
}
