using Danidrum.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Danidrum.UserControls;

public class MeasuresControl : Control
{
    private Typeface _typeface;
    private Pen _measurePen;
    private Pen _subdivisionPen;

    public static readonly DependencyProperty SongProperty = DependencyProperty.Register(nameof(Song), typeof(SongContext), typeof(MeasuresControl), new PropertyMetadata(null, PropertyChangedCallback));
    public SongContext Song
    {
        get => (SongContext)GetValue(SongProperty);
        set => SetValue(SongProperty, value);
    }

    public static readonly DependencyProperty PixelPerMsProperty = DependencyProperty.Register(nameof(PixelPerMs), typeof(double), typeof(MeasuresControl), new PropertyMetadata(0d, PropertyChangedCallback));
    public double PixelPerMs
    {
        get => (double)GetValue(PixelPerMsProperty);
        set => SetValue(PixelPerMsProperty, value);
    }

    public static readonly DependencyProperty MeasureBrushProperty = DependencyProperty.Register(nameof(MeasureBrush), typeof(Brush), typeof(MeasuresControl), new PropertyMetadata(null, PropertyChangedCallback));
    public Brush MeasureBrush
    {
        get => (Brush)GetValue(MeasureBrushProperty);
        set => SetValue(MeasureBrushProperty, value);
    }

    public static readonly DependencyProperty SubdivisionBrushProperty = DependencyProperty.Register(nameof(SubdivisionBrush), typeof(Brush), typeof(MeasuresControl), new PropertyMetadata(null, PropertyChangedCallback));
    public Brush SubdivisionBrush
    {
        get => (Brush)GetValue(SubdivisionBrushProperty);
        set => SetValue(SubdivisionBrushProperty, value);
    }

    public static readonly DependencyProperty MeasureThicknessProperty = DependencyProperty.Register(nameof(MeasureThickness), typeof(double), typeof(MeasuresControl), new PropertyMetadata(0d, PropertyChangedCallback));
    public double MeasureThickness
    {
        get => (double)GetValue(MeasureThicknessProperty);
        set => SetValue(MeasureThicknessProperty, value);
    }

    public static readonly DependencyProperty SubdivisionThicknessProperty = DependencyProperty.Register(nameof(SubdivisionThickness), typeof(double), typeof(MeasuresControl), new PropertyMetadata(0d, PropertyChangedCallback));
    public double SubdivisionThickness
    {
        get => (double)GetValue(SubdivisionThicknessProperty);
        set => SetValue(SubdivisionThicknessProperty, value);
    }

                                           
    public static readonly DependencyProperty IsMeasureInfoVisibleProperty = DependencyProperty.Register(nameof(IsMeasureInfoVisible), typeof(bool), typeof(MeasuresControl), new PropertyMetadata(false, PropertyChangedCallback));
    public bool IsMeasureInfoVisible
    {
        get => (bool)GetValue(IsMeasureInfoVisibleProperty);
        set => SetValue(IsMeasureInfoVisibleProperty, value);
    }

    private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = ((MeasuresControl)d);

        ctrl._measurePen = ctrl.MeasureBrush == null || ctrl.MeasureThickness == 0 ? null : new Pen(ctrl.MeasureBrush, ctrl.MeasureThickness);
        ctrl._subdivisionPen = ctrl.SubdivisionBrush == null || ctrl.SubdivisionThickness == 0 ? null : new Pen(ctrl.SubdivisionBrush, ctrl.SubdivisionThickness);
        ctrl._typeface = !ctrl.IsMeasureInfoVisible ? null : new Typeface(ctrl.FontFamily, ctrl.FontStyle, ctrl.FontWeight, ctrl.FontStretch, ctrl.FontFamily);

        ctrl.InvalidateVisual();
    } 

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        
        if (Song == null)
            return;

        var measurePen = new Pen(MeasureBrush, MeasureThickness);
        var subdivisionPen = new Pen(SubdivisionBrush, SubdivisionThickness);

        foreach (var measure in Song.Measures)
        {
            var measurePosition = PixelPerMs * measure.StartTimeMs;
            var subdivisionOffset = PixelPerMs * (measure.LengthMs / measure.TimeSignature.Denominator);

            if (_measurePen != null)
            {
                dc.DrawLine(_measurePen,
                    point0: new Point(measurePosition, 0),
                    point1: new Point(measurePosition, ActualHeight));
            }

            var subdivisions = measure.TimeSignature.Denominator;

            if (_subdivisionPen != null)
            {
                for (var i = 1; i < subdivisions; i++)
                {
                    var subdivisionPosition = measurePosition + subdivisionOffset * i;

                    dc.DrawLine(subdivisionPen,
                        point0: new Point(subdivisionPosition, 0),
                        point1: new Point(subdivisionPosition, ActualHeight));
                }
            }

            if (IsMeasureInfoVisible)
            {
                var formattedText = new FormattedText(
                    $"{measure.MeasureIndex}. [{measure.TimeSignature.Numerator}/{measure.TimeSignature.Denominator}]",
                    CultureInfo.InvariantCulture, 
                    FlowDirection,
                    _typeface,
                    FontSize, // Font size (in Device Independent Pixels)
                    Foreground, // Text brush/color
                    VisualTreeHelper.GetDpi(this).PixelsPerDip // DPI scaling factor
                );

                dc.DrawText(formattedText, new Point(measurePosition + 10, 7));
            }


        }
    }


    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        InvalidateVisual();
    }
}
