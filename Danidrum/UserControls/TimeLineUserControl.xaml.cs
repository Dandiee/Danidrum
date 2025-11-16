
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

    public static readonly DependencyProperty SongProperty = DependencyProperty.Register(nameof(Song), typeof(SongContext), typeof(TimeLineUserControl), new PropertyMetadata(default(SongContext), PropertyChangedCallback));

    private double _pxPerMs;
    private List<double> _ticks;

    private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as TimeLineUserControl;

        control._pxPerMs = control.TimeLineContainer.ActualWidth / control.Song.LengthMs;
        

        control.RangeEndMs = control.Song.LengthMs - control.RangeStart / control.TimeLineContainer.ActualWidth * control.RangeStart;
    }

    public SongContext Song
    {
        get => (SongContext)GetValue(SongProperty);
        set => SetValue(SongProperty, value);
    }

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(TimeLineUserControl), new PropertyMetadata(false));
    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
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

        ctrl._pxPerMs = ctrl.Song.LengthMs / ctrl.TimeLineContainer.ActualWidth;
        var visibleArea = ctrl.VisibleAreaMs / ctrl._pxPerMs;

        ctrl.CurrentPosition = ctrl.TimeLineContainer.ActualWidth * (ctrl.CurrentTimeMs / ctrl.Song.LengthMs);
        ctrl.LeftMaskWidth = ctrl.CurrentPosition - visibleArea / 2;
        ctrl.RightMaskWidth = ctrl.TimeLineContainer.ActualWidth - (visibleArea + ctrl.LeftMaskWidth);
        ctrl.RangeStart = ctrl.RangeStartMs / ctrl._pxPerMs;
        ctrl.RangeEnd = (ctrl.Song.LengthMs - ctrl.RangeEndMs)/ ctrl._pxPerMs;
        ctrl._ticks = ctrl.Song.Measures.Select(e => e.StartTimeMs / ctrl._pxPerMs).ToList();

    }

    private void PositionThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var pxPerMs = TimeLineContainer.ActualWidth / Song.LengthMs;
        var deltaTime = e.HorizontalChange / pxPerMs;
        var targetTimeMs = CurrentTimeMs + deltaTime;


        CurrentTimeMs = Math.Clamp(targetTimeMs, 0, Song.LengthMs);
    }


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

    private bool _originalIsPlaying;
    private bool _isSeeking;
    private void OnSeekStarted(object sender, EventArgs e)
    {
        _originalIsPlaying = IsPlaying;
        IsPlaying = false;
        _isSeeking = true;
    }

    private void OnSeekCompleted(object sender, MouseEventArgs e)
    {
        if (_isSeeking)
        {
            IsUserSeeking = true;
            var ratio = e.MouseDevice.GetPosition(TimeLineContainer).X / TimeLineContainer.ActualWidth;
            var targetTime = Song.LengthMs * ratio;
            CurrentTimeMs = Math.Clamp(targetTime, RangeStartMs, RangeEndMs);
            IsUserSeeking = false;

            _isSeeking = false;
            IsPlaying = _originalIsPlaying;
        }
    }

    private void TimeLineUserControl_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isSeeking)
        {
            IsUserSeeking = true;
            var ratio = e.MouseDevice.GetPosition(TimeLineContainer).X / TimeLineContainer.ActualWidth;
            var targetTime = Song.LengthMs * ratio;
            CurrentTimeMs = Math.Clamp(targetTime, RangeStartMs, RangeEndMs);
            IsUserSeeking = false;
        }
    }

    public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(nameof(RangeStart), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d));
    public double RangeStart
    {
        get => (double)GetValue(RangeStartProperty);
        set => SetValue(RangeStartProperty, value);
    }

    public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(nameof(RangeEnd), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d));
    public double RangeEnd
    {
        get => (double)GetValue(RangeEndProperty);
        set => SetValue(RangeEndProperty, value);
    }


    private void OnRangeDragDelta(object sender, DragDeltaEventArgs e)
    {
        IsPlaying = false;
        IsUserSeeking = true;
        _isSeeking = true;

        if (sender == StartRangeThumb)
        {
            var nearest = NearestTick(Math.Clamp(e.HorizontalChange + RangeStart, 0, TimeLineContainer.ActualWidth - RangeEnd));
            if (nearest < TimeLineContainer.ActualWidth - RangeEnd)
            {
                RangeStart = NearestTick(Math.Clamp(e.HorizontalChange + RangeStart, 0, TimeLineContainer.ActualWidth - RangeEnd));
                RangeStartMs = RangeStart / TimeLineContainer.ActualWidth * Song.LengthMs;
            }
        }
        else
        {
            var nearest = NearestTick(Math.Clamp(RangeEnd - e.HorizontalChange, 0, TimeLineContainer.ActualWidth - RangeStart));
            if (TimeLineContainer.ActualWidth - nearest > RangeStart)
            {
                RangeEnd = NearestTick(Math.Clamp(RangeEnd - e.HorizontalChange, 0, TimeLineContainer.ActualWidth - RangeStart));
                RangeEndMs = Song.LengthMs - RangeEnd / TimeLineContainer.ActualWidth * Song.LengthMs;
            }
        }

        if (RangeStartMs >= RangeEndMs)
        {

        }

        CurrentTimeMs = Math.Clamp(CurrentTimeMs, RangeStartMs, RangeEndMs);

        _isSeeking = false;
        IsUserSeeking = false;
    }

    public static readonly DependencyProperty RangeStartMsProperty = DependencyProperty.Register(nameof(RangeStartMs), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d));
    public double RangeStartMs
    {
        get => (double)GetValue(RangeStartMsProperty);
        set => SetValue(RangeStartMsProperty, value);
    }

    public static readonly DependencyProperty RangeEndMsProperty = DependencyProperty.Register(nameof(RangeEndMs), typeof(double), typeof(TimeLineUserControl), new PropertyMetadata(0d));
    public double RangeEndMs
    {
        get => (double)GetValue(RangeEndMsProperty);
        set => SetValue(RangeEndMsProperty, value);
    }

    private double NearestTick(double value) => _ticks.MinBy(e => Math.Abs(value - e));

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        //RangeStart = _ RangeStartMs
    }
}
