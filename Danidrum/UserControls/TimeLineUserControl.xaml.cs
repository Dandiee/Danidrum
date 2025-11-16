
using Danidrum.Services;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Danidrum.UserControls;
public partial class TimeLineUserControl
{
    public TimeLineUserControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty CurrentTimeMsProperty = DependencyProperty.Register(nameof(CurrentTimeMs), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d, OnPropertyChanged));
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

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = d as TimeLineUserControl;
        var pixelPerMs = ctrl.Song.LengthMs / ctrl.TimeLineContainer.ActualWidth;
        var visibleArea = ctrl.VisibleAreaMs / pixelPerMs;

        ctrl.CurrentPosition = ctrl.TimeLineContainer.ActualWidth * (ctrl.CurrentTimeMs / ctrl.Song.LengthMs);
        ctrl.LeftMaskWidth = ctrl.CurrentPosition - visibleArea / 2;
        ctrl.RightMaskWidth = ctrl.TimeLineContainer.ActualWidth - (visibleArea + ctrl.LeftMaskWidth);
    }

    private void PositionThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var pxPerMs = TimeLineContainer.ActualWidth / Song.LengthMs;
        var deltaTime = e.HorizontalChange / pxPerMs;
        var targetTimeMs = CurrentTimeMs + deltaTime;

        
        CurrentTimeMs = Math.Clamp(targetTimeMs, 0, Song.LengthMs);
    }

    private void PositionThumb_OnDragStarted(object sender, DragStartedEventArgs e) => IsUserSeeking = true;
    private void PositionThumb_OnDragCompleted(object sender, DragCompletedEventArgs e) => IsUserSeeking = false;


    public static readonly DependencyProperty VisibleAreaMsProperty = DependencyProperty.Register(nameof(VisibleAreaMs), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d, OnPropertyChanged));
    public double VisibleAreaMs
    {
        get => (double)GetValue(VisibleAreaMsProperty);
        set => SetValue(VisibleAreaMsProperty, value);
    }

    public static readonly DependencyProperty LeftMaskWidthProperty = DependencyProperty.Register(nameof(LeftMaskWidth), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(default(double)));
    public double LeftMaskWidth
    {
        get => (double)GetValue(LeftMaskWidthProperty);
        set => SetValue(LeftMaskWidthProperty, value);
    }

    public static readonly DependencyProperty RightMaskWidthProperty = DependencyProperty.Register(nameof(RightMaskWidth), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(default(double)));
    public double RightMaskWidth
    {
        get => (double)GetValue(RightMaskWidthProperty);
        set => SetValue(RightMaskWidthProperty, value);
    }

    private void TimeLineContainer_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        IsUserSeeking = true;
        var ratio = e.MouseDevice.GetPosition(TimeLineContainer).X / TimeLineContainer.ActualWidth;
        var targetTime = Song.LengthMs * ratio;
        CurrentTimeMs = targetTime;
        IsUserSeeking = false;
    }
}
