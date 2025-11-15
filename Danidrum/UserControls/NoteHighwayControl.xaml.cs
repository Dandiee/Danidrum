using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Danidrum.Services;

namespace Danidrum.UserControls;

public class MidLineConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double viewportWidth)
        {
            return new Thickness(viewportWidth / 2.0, 0, 0, 0);
        }

        return new Thickness();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class NoteHighwayControl
{
    public static double PixelPerSecond = 300;
    public static double PixelPerMs = PixelPerSecond / 1000;
    public static double VisualLatency = 400;

    public NoteHighwayControl()
    {
        InitializeComponent();
    }

    
    public static readonly DependencyProperty ChunkProperty = DependencyProperty.Register(nameof(Chunk), typeof(ChunkContext), typeof(NoteHighwayControl), new PropertyMetadata(null, OnChunkPropertyChanged));
    public ChunkContext Chunk
    {
        get => (ChunkContext)GetValue(ChunkProperty);
        set => SetValue(ChunkProperty, value);
    }
    private static void OnChunkPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.RebuildLanes();
    }

    private void NoteCanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e) => RebuildLanes();

  
    private void RebuildLanes()
    {
        if (Chunk == null) return;

        LanesGrid.RowDefinitions.Clear();
        LanesGrid.Children.Clear();

        LaneNamesGrid.RowDefinitions.Clear();
        LaneNamesGrid.Children.Clear();

        LanesGrid.Width = PixelPerMs * Chunk.Song.LengthMs;

        var backgroundCanvas = new Canvas();
        LanesGrid.Children.Add(backgroundCanvas);
        Grid.SetRowSpan(backgroundCanvas, Chunk.Lanes.Count);

        foreach (var measure in Chunk.Song.Measures)
        {
            var measurePosition = PixelPerMs * measure.StartTimeMs;
            var subdivisionOffset = PixelPerMs * (measure.LengthMs / measure.TimeSignature.Denominator);
            backgroundCanvas.Children.Add(new Line
            {
                X1 = measurePosition,
                X2 = measurePosition,
                Y1 = 0,
                Y2 = LanesGrid.ActualHeight,

                Stroke = Brushes.White,
            });

            var subdivisions = measure.TimeSignature.Denominator;

            for (var i = 1; i < subdivisions; i++)
            {
                var subdivisionPosition = measurePosition + subdivisionOffset * i;
                backgroundCanvas.Children.Add(new Line
                {
                    X1 = subdivisionPosition,
                    X2 = subdivisionPosition,
                    Y1 = 0,
                    Y2 = LanesGrid.ActualHeight,

                    Stroke = Brushes.DarkGray,
                });
            }

            var measureInfo = new TextBlock
            {
                Text = $"{measure.MeasureIndex}. [{measure.TimeSignature.Numerator}/{measure.TimeSignature.Denominator}]",
            };
            backgroundCanvas.Children.Add(measureInfo);
            Canvas.SetLeft(measureInfo, measurePosition + 10);
            Canvas.SetTop(measureInfo, 0);
        }

        LanesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
        LaneNamesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });

        var laneNameTemplate = TryFindResource("LaneNameTemplate") as DataTemplate;
        var laneHeight = ActualHeight / Chunk.Lanes.Count;

        for (var i = 0; i < Chunk.Lanes.Count; i++)
        {
            var lane = Chunk.Lanes[i];

            LanesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            LaneNamesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var laneControl = new NoteLaneControl(lane)
            {
                Height = laneHeight
            };
            Grid.SetRow(laneControl, i + 1);
            LanesGrid.Children.Add(laneControl);
            
            var titleHost = new ContentControl
            {
                Content = lane,
                ContentTemplate = laneNameTemplate,
                VerticalAlignment = VerticalAlignment.Top,
                Height = laneHeight
            };

            Grid.SetRow(titleHost, i + 1);
            LaneNamesGrid.Children.Add(titleHost);
        }
    }

    public static readonly DependencyProperty CurrentTimeMsProperty = DependencyProperty.Register(nameof(CurrentTimeMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(0.0, OnCurrentTimeMsChanged));
    public double CurrentTimeMs
    {
        get => (double)GetValue(CurrentTimeMsProperty);
        set => SetValue(CurrentTimeMsProperty, value);
    }
    private static void OnCurrentTimeMsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;

        if (control.NoteCanvasScroller == null || control.Chunk == null || control.Chunk.Song.LengthMs == 0) return;

        var ratio = (control.CurrentTimeMs - VisualLatency) / control.Chunk.Song.LengthMs;
        var here = (control.NoteCanvasScroller.ExtentWidth - control.NoteCanvasScroller.ActualWidth / 2) * ratio;
        control.NoteCanvasScroller.ScrollToHorizontalOffset(here);
    }
}
