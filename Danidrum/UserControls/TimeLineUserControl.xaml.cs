
using Danidrum.Services;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Danidrum.UserControls;
public partial class TimeLineUserControl
{
    public TimeLineUserControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty CurrentTimeMsProperty = DependencyProperty.Register(nameof(CurrentTimeMs), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d, OnCurrentTimeMsPropertyChanged));
    public double CurrentTimeMs
    {
        get => (double)GetValue(CurrentTimeMsProperty);
        set => SetValue(CurrentTimeMsProperty, value);
    }

    public static readonly DependencyProperty CurrentPositionProperty = DependencyProperty.Register(nameof(CurrentPosition), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d));
    public double CurrentPosition
    {
        get => (double)GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    public static readonly DependencyProperty SongProperty = DependencyProperty.Register(nameof(Song), typeof(SongContext), typeof(TimeLineUserControl), new PropertyMetadata(default(SongContext)));
    public SongContext Song
    {
        get => (SongContext)GetValue(SongProperty);
        set => SetValue(SongProperty, value);
    }

    public static readonly DependencyProperty IsUserSeekingProperty = DependencyProperty.Register(nameof(IsUserSeeking), typeof(bool), typeof(TimeLineUserControl), new PropertyMetadata(false));
    public bool IsUserSeeking
    {
        get => (bool)GetValue(IsUserSeekingProperty);
        set => SetValue(IsUserSeekingProperty, value);
    }

    private static void OnCurrentTimeMsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = d as TimeLineUserControl;
        ctrl.CurrentPosition = ctrl.ActualWidth * (ctrl.CurrentTimeMs / ctrl.Song.LengthMs);
    }

    private void PositionThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var pxPerMs = ActualWidth / Song.LengthMs;
        var deltaTime = e.HorizontalChange / pxPerMs;

        CurrentTimeMs += deltaTime;
    }

    private void PositionThumb_OnDragStarted(object sender, DragStartedEventArgs e) => IsUserSeeking = true;
    private void PositionThumb_OnDragCompleted(object sender, DragCompletedEventArgs e) => IsUserSeeking = false;
}
