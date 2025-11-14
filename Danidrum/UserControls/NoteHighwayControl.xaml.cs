using System;
using System.Windows;
using System.Windows.Controls;

namespace Danidrum.UserControls;

public partial class NoteHighwayControl : UserControl
{
    // prevent recursive sync
    private bool _isSyncingScroll = false;

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
        control.UpdateLaneSizes();
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

    private void UpdateLaneSizes()
    {
        // Prefer viewport height if available, else fallback to ActualHeight
        double totalHeight = NoteCanvasScroller?.ViewportHeight > 0
            ? NoteCanvasScroller.ViewportHeight
            : ActualHeight;

        if (LanesCount <= 0) return;

        LaneHeight = totalHeight / LanesCount;
        NoteHeight = Math.Max(4, LaneHeight - 10); // leave a small margin, min height 4
    }

    // When the scroller is resized (e.g., window resized)
    private void NoteCanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Update lane sizes based on viewport
        UpdateLaneSizes();

        // Update padding so content won't be clipped at edges (optional small left padding)
        // Keeping left padding 0 keeps absolute pixel mapping simple.
        // If you want to center playhead visually, the scroll math uses viewport width.
    }

    // Center playhead in viewport (clamped)
    private void UpdateScrollPosition()
    {
        if (NoteCanvasScroller == null) return;

        double playheadPixelPos = CurrentTimeMs * PxPerMs;

        // Center on playhead:
        double viewportWidth = NoteCanvasScroller.ViewportWidth;
        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            // If viewport not ready, do a best-effort scroll to exact pixel
            NoteCanvasScroller.ScrollToHorizontalOffset(playheadPixelPos);
            return;
        }

        double targetOffset = playheadPixelPos - (viewportWidth / 2.0);

        // Clamp between 0 and maximum scrollable width
        double maxOffset = Math.Max(0, SongTotalWidth - viewportWidth);
        if (targetOffset < 0) targetOffset = 0;
        if (targetOffset > maxOffset) targetOffset = maxOffset;

        NoteCanvasScroller.ScrollToHorizontalOffset(targetOffset);
    }

    // Sync vertical scroll between lane names and canvas
    private void NoteCanvasScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 && !_isSyncingScroll)
        {
            _isSyncingScroll = true;
            LaneNameScroller.ScrollToVerticalOffset(e.VerticalOffset);
            _isSyncingScroll = false;
        }
    }

    private void LaneNameScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 && !_isSyncingScroll)
        {
            _isSyncingScroll = true;
            NoteCanvasScroller.ScrollToVerticalOffset(e.VerticalOffset);
            _isSyncingScroll = false;
        }
    }
}
