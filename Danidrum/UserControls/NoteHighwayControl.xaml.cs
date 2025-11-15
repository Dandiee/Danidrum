using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections;
using System.Collections.Specialized;
using Danidrum.ViewModels;
using System.Linq;

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

public partial class NoteHighwayControl : UserControl
{
    public NoteHighwayControl()
    {
        InitializeComponent();
    }

    // NoteLanes dependency property (collection of NoteLaneViewModel)
    public static readonly DependencyProperty NoteLanesProperty = DependencyProperty.Register(
        nameof(NoteLanes), typeof(IEnumerable), typeof(NoteHighwayControl),
        new PropertyMetadata(null, OnNoteLanesChanged));

    public IEnumerable NoteLanes
    {
        get => (IEnumerable)GetValue(NoteLanesProperty);
        set => SetValue(NoteLanesProperty, value);
    }

    private static void OnNoteLanesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.UnsubscribeFromNoteLanes(e.OldValue as INotifyCollectionChanged);
        control.SubscribeToNoteLanes(e.NewValue as INotifyCollectionChanged);
        control.RebuildLanes();
    }

    private void SubscribeToNoteLanes(INotifyCollectionChanged? coll)
    {
        if (coll != null)
            coll.CollectionChanged += NoteLanes_CollectionChanged;
    }

    private void UnsubscribeFromNoteLanes(INotifyCollectionChanged? coll)
    {
        if (coll != null)
            coll.CollectionChanged -= NoteLanes_CollectionChanged;
    }

    private void NoteLanes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Simple approach: rebuild all rows on change
        Dispatcher.InvokeAsync(RebuildLanes);
    }

    private void RebuildLanes()
    {
        if (LanesGrid == null || LaneNamesGrid == null) return;

        LanesGrid.RowDefinitions.Clear();
        LanesGrid.Children.Clear();

        LaneNamesGrid.RowDefinitions.Clear();
        LaneNamesGrid.Children.Clear();

        if (NoteLanes == null) return;

        var lanes = NoteLanes.Cast<NoteLaneViewModel>().ToList();

        LanesCount = lanes.Count;

        // Try get the template once
        var laneNameTemplate = TryFindResource("LaneNameTemplate") as DataTemplate;

        for (int i =0; i < lanes.Count; i++)
        {
            var lane = lanes[i];

            // create row in both grids
            var rd = new RowDefinition { Height = new GridLength(LaneHeight) };
            LanesGrid.RowDefinitions.Add(rd);
            LaneNamesGrid.RowDefinitions.Add(new RowDefinition { Height = rd.Height });

            // create NoteLaneControl
            var laneCtrl = new NoteLaneControl()
            {
                Notes = lane.Notes,
                NoteHeight = NoteHeight,
                Height = LaneHeight,
                Width = SongTotalWidth,
                VerticalAlignment = VerticalAlignment.Top
            };

            Grid.SetRow(laneCtrl, i);
            LanesGrid.Children.Add(laneCtrl);

            // create a ContentControl that uses the LaneNameTemplate (defined in XAML)
            ContentControl titleHost;
            if (laneNameTemplate != null)
            {
                titleHost = new ContentControl
                {
                    Content = lane,
                    ContentTemplate = laneNameTemplate
                };
            }
            else
            {
                // fallback to simple TextBlock if template not found
                titleHost = new ContentControl
                {
                    Content = lane,
                    ContentTemplate = null
                };
                var tb = new TextBlock();
                tb.SetBinding(TextBlock.TextProperty, new Binding(nameof(NoteLaneViewModel.LaneName)) { Source = lane });
                titleHost.Content = tb;
            }

            // Ensure the host fills the row height
            titleHost.VerticalAlignment = VerticalAlignment.Top;
            titleHost.Height = LaneHeight;

            Grid.SetRow(titleHost, i);
            LaneNamesGrid.Children.Add(titleHost);
        }
    }

    // Pixels per second (zoom)
    public static readonly DependencyProperty PixelsPerSecondProperty =
        DependencyProperty.Register(nameof(PixelsPerSecond), typeof(double), typeof(NoteHighwayControl),
            new PropertyMetadata(50.0, OnPixelsPerSecondChanged));

    public double PixelsPerSecond
    {
        get => (double)GetValue(PixelsPerSecondProperty);
        set => SetValue(PixelsPerSecondProperty, value);
    }

    private static void OnPixelsPerSecondChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.UpdateScrollPosition();
    }

    // Current playhead time in milliseconds
    public static readonly DependencyProperty CurrentTimeMsProperty =
        DependencyProperty.Register(nameof(CurrentTimeMs), typeof(double), typeof(NoteHighwayControl),
            new PropertyMetadata(0.0, OnCurrentTimeMsChanged));

    public double CurrentTimeMs
    {
        get => (double)GetValue(CurrentTimeMsProperty);
        set => SetValue(CurrentTimeMsProperty, value);
    }

    private static void OnCurrentTimeMsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.UpdateScrollPosition();
    }

    // total width of the song in pixels
    public static readonly DependencyProperty SongTotalWidthProperty =
        DependencyProperty.Register(nameof(SongTotalWidth), typeof(double), typeof(NoteHighwayControl),
            new PropertyMetadata(1000.0, OnSongTotalWidthChanged));

    public double SongTotalWidth
    {
        get => (double)GetValue(SongTotalWidthProperty);
        set => SetValue(SongTotalWidthProperty, value);
    }

    private static void OnSongTotalWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        // propagate width to lane children
        control.UpdateLaneWidths();
    }

    public static readonly DependencyProperty TotalSongDurationMsProperty = DependencyProperty.Register(
        nameof(TotalSongDurationMs), typeof(double), typeof(NoteHighwayControl), new PropertyMetadata(default(double)));
    public double TotalSongDurationMs
    {
        get { return (double)GetValue(TotalSongDurationMsProperty); }
        set { SetValue(TotalSongDurationMsProperty, value); }
    }

    public static readonly DependencyProperty NotePaddingProperty =
        DependencyProperty.Register(nameof(NotePadding), typeof(Thickness), typeof(NoteHighwayControl),
            new PropertyMetadata(default(Thickness)));

    public Thickness NotePadding
    {
        get => (Thickness)GetValue(NotePaddingProperty);
        set => SetValue(NotePaddingProperty, value);
    }

    public static readonly DependencyProperty LanesCountProperty =
        DependencyProperty.Register(nameof(LanesCount), typeof(int), typeof(NoteHighwayControl),
            new PropertyMetadata(1, OnLanesCountChanged));

    public int LanesCount
    {
        get => (int)GetValue(LanesCountProperty);
        set => SetValue(LanesCountProperty, value);
    }

    private static void OnLanesCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.UpdateLaneSizes(new Size(control.NoteCanvasScroller.ActualWidth, control.NoteCanvasScroller.ActualHeight));
    }

    public static readonly DependencyProperty LaneHeightProperty =
        DependencyProperty.Register(nameof(LaneHeight), typeof(double), typeof(NoteHighwayControl),
            new PropertyMetadata(40.0, OnLaneHeightChanged));

    public double LaneHeight
    {
        get => (double)GetValue(LaneHeightProperty);
        set => SetValue(LaneHeightProperty, value);
    }

    private static void OnLaneHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.UpdateLaneHeights();
    }

    public static readonly DependencyProperty NoteHeightProperty =
        DependencyProperty.Register(nameof(NoteHeight), typeof(double), typeof(NoteHighwayControl),
            new PropertyMetadata(30.0, OnNoteHeightChanged));

    public double NoteHeight
    {
        get => (double)GetValue(NoteHeightProperty);
        set => SetValue(NoteHeightProperty, value);
    }

    private static void OnNoteHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteHighwayControl)d;
        control.UpdateNoteHeights();
    }

    // Helper: pixels per millisecond (shared conversion)
    private double PxPerMs => PixelsPerSecond / 1000.0;

    private void UpdateLaneSizes(Size newSize)
    {
        // Prefer viewport height if available, else fallback to ActualHeight
        double totalHeight = NoteCanvasScroller?.ViewportHeight > 0
            ? NoteCanvasScroller.ViewportHeight
            : ActualHeight;

        totalHeight = newSize.Height;

        if (LanesCount <= 0) return;

        LaneHeight = totalHeight / LanesCount;
        NoteHeight = Math.Max(4, LaneHeight - 10); // leave a small margin, min height 4

        UpdateLaneHeights();
    }

    private void UpdateLaneHeights()
    {
        if (LanesGrid == null || LaneNamesGrid == null) return;

        for (int i =0; i < LanesGrid.RowDefinitions.Count; i++)
        {
            LanesGrid.RowDefinitions[i].Height = new GridLength(LaneHeight);
        }

        for (int i =0; i < LaneNamesGrid.RowDefinitions.Count; i++)
        {
            LaneNamesGrid.RowDefinitions[i].Height = new GridLength(LaneHeight);
        }

        foreach (var child in LanesGrid.Children.OfType<NoteLaneControl>())
        {
            child.Height = LaneHeight;
            child.NoteHeight = NoteHeight;
        }

        foreach (var child in LaneNamesGrid.Children.OfType<ContentControl>())
        {
            child.Height = LaneHeight;
        }
    }

    private void UpdateNoteHeights()
    {
        if (LanesGrid == null) return;
        foreach (var child in LanesGrid.Children.OfType<NoteLaneControl>())
        {
            child.NoteHeight = NoteHeight;
        }
    }

    private void UpdateLaneWidths()
    {
        if (LanesGrid == null) return;
        foreach (var child in LanesGrid.Children.OfType<NoteLaneControl>())
        {
            child.Width = SongTotalWidth;
        }
    }

    private void NoteCanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLaneSizes(e.NewSize);
    }

    // Center playhead in viewport (clamped)
    private void UpdateScrollPosition()
    {
        if (NoteCanvasScroller == null || TotalSongDurationMs == 0) return;

        var ratio = CurrentTimeMs / TotalSongDurationMs;
        var here = (NoteCanvasScroller.ExtentWidth - (NoteCanvasScroller.ActualWidth / 2)) * ratio;
        NoteCanvasScroller.ScrollToHorizontalOffset(here);

    }
}
