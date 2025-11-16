using System.CodeDom;
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
    private readonly SolidColorBrush BarBrush;
    private readonly SolidColorBrush SubdivisionBrush;
    private const double MeasureInfoHeight = 30;

    public NoteHighwayControl()
    {
        BarBrush = FindResource("MaterialDesign.Brush.Primary.Foreground") as SolidColorBrush;
        SubdivisionBrush = FindResource("MaterialDesign.Brush.Primary.Dark.Foreground") as SolidColorBrush;

        InitializeComponent();
    }

    public static readonly DependencyProperty PixelPerMsProperty = DependencyProperty.Register(nameof(PixelPerMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(0d, OnPixelPerMsPropertyChanged));
    private static void OnPixelPerMsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.RebuildLanes();
    }
    public double PixelPerMs
    {
        get => (double)GetValue(PixelPerMsProperty);
        set => SetValue(PixelPerMsProperty, value);
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


    public static readonly DependencyProperty RangeStartMsProperty = DependencyProperty.Register(nameof(RangeStartMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(0d, OnChunkPropertyChanged));
    public double RangeStartMs
    {
        get => (double)GetValue(RangeStartMsProperty);
        set => SetValue(RangeStartMsProperty, value);
    }


    public static readonly DependencyProperty RangeEndMsProperty = DependencyProperty.Register(nameof(RangeEndMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(0d, OnChunkPropertyChanged));
    public double RangeEndMs
    {
        get => (double)GetValue(RangeEndMsProperty);
        set => SetValue(RangeEndMsProperty, value);
    }

    private void NoteCanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e) => RebuildLanes();

  
    private void RebuildLanes()
    {
        if (Chunk == null) return;

        LanesGrid.LayoutUpdated += ApplyScrollAnchor;

        VisibleAreaMs = NoteCanvasScroller.ActualWidth / PixelPerMs;

        LanesGrid.RowDefinitions.Clear();
        LanesGrid.Children.Clear();

        LaneNamesGrid.RowDefinitions.Clear();
        LaneNamesGrid.Children.Clear();

        LanesGrid.Width = PixelPerMs * Chunk.Channel.Song.LengthMs;

        var backgroundCanvas = new Canvas();
        LanesGrid.Children.Add(backgroundCanvas);
        Grid.SetRowSpan(backgroundCanvas, Chunk.Lanes.Count);

        LanesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MeasureInfoHeight) });
        LaneNamesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MeasureInfoHeight) });

        var laneNameTemplate = TryFindResource("LaneNameTemplate") as DataTemplate;
        var laneHeight = Math.Max(10, (ActualHeight - MeasureInfoHeight) / Chunk.Lanes.Count);

        for (var i = 0; i < Chunk.Lanes.Count; i++)
        {
            var lane = Chunk.Lanes[i];

            LanesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            LaneNamesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var laneControl = new NoteLaneControl(lane, this)
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


        if (RangeStartMs > 0)
        {
            var startRangeOverlay = new Grid
            {
                Background = Brushes.Black,
                Width = RangeStartMs * PixelPerMs,
                HorizontalAlignment = HorizontalAlignment.Left,
                Opacity = 0.4
            };

            Grid.SetRowSpan(startRangeOverlay, Chunk.Lanes.Count + 1);
            LanesGrid.Children.Add(startRangeOverlay);
        }

        if (RangeEndMs < Song.LengthMs)
        {
            var distance = Song.LengthMs - RangeEndMs;
            var endRangeOverlay = new Grid
            {
                Background = Brushes.Black,
                Width = distance * PixelPerMs,
                HorizontalAlignment = HorizontalAlignment.Right,
                Opacity = 0.4
            };

            Grid.SetRowSpan(endRangeOverlay, Chunk.Lanes.Count + 1);
            LanesGrid.Children.Add(endRangeOverlay);
        }

        // ApplyScrollAnchor runs after the layout is updated

    }

    private void ApplyScrollAnchor(object? sender, EventArgs e)
    {
        LanesGrid.LayoutUpdated -= ApplyScrollAnchor;
        double newCenterPixel = CurrentTimeMs * PixelPerMs;
        double newHorizontalOffset = newCenterPixel;
        NoteCanvasScroller.ScrollToHorizontalOffset(newHorizontalOffset);
    }

    public static readonly DependencyProperty SongProperty = DependencyProperty.Register(nameof(Song), typeof(SongContext), typeof(NoteHighwayControl), new PropertyMetadata(default(SongContext)));
    public SongContext Song
    {
        get => (SongContext)GetValue(SongProperty);
        set => SetValue(SongProperty, value);
    }

    public static readonly DependencyProperty VisibleAreaMsProperty = DependencyProperty.Register(nameof(VisibleAreaMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(0d));
    public double VisibleAreaMs
    {
        get => (double)GetValue(VisibleAreaMsProperty);
        set => SetValue(VisibleAreaMsProperty, value);
    }

    public static readonly DependencyProperty VisualLatencyInMsProperty = DependencyProperty.Register(nameof(VisualLatencyInMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(0d));
    public double VisualLatencyInMs
    {
        get => (double)GetValue(VisualLatencyInMsProperty);
        set => SetValue(VisualLatencyInMsProperty, value);
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

        if (control.NoteCanvasScroller == null || control.Chunk == null || control.Chunk.Channel.Song.LengthMs == 0) return;

        var ratio = (control.CurrentTimeMs /*- control.VisualLatencyInMs*/) / control.Chunk.Channel.Song.LengthMs;
        var here = (control.NoteCanvasScroller.ExtentWidth - control.NoteCanvasScroller.ActualWidth / 2) * ratio;
        control.NoteCanvasScroller.ScrollToHorizontalOffset(here);
    }
}
