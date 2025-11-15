using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            new PropertyMetadata(1000.0));

    public double SongTotalWidth
    {
        get => (double)GetValue(SongTotalWidthProperty);
        set => SetValue(SongTotalWidthProperty, value);
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
            new PropertyMetadata(40.0));

    public double LaneHeight
    {
        get => (double)GetValue(LaneHeightProperty);
        set => SetValue(LaneHeightProperty, value);
    }

    public static readonly DependencyProperty NoteHeightProperty =
        DependencyProperty.Register(nameof(NoteHeight), typeof(double), typeof(NoteHighwayControl),
            new PropertyMetadata(30.0));

    public double NoteHeight
    {
        get => (double)GetValue(NoteHeightProperty);
        set => SetValue(NoteHeightProperty, value);
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

    private bool _isSyncingScroll;
    private void NoteCanvasScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 && !_isSyncingScroll)
        {
            _isSyncingScroll = true;
            LaneNameScroller.ScrollToVerticalOffset(e.VerticalOffset);
            _isSyncingScroll = false;
        }
    }
}
