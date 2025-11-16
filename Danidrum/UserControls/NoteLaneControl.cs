using System.CodeDom;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Danidrum.Services;

namespace Danidrum.UserControls;

public class NoteLaneControl : FrameworkElement
{
    public const double PerfectHitLimitMs = 75;
    public const double ImperfectHitLimitMs = 110;

    private readonly LaneContext _lane;
    private readonly NoteHighwayControl _owner;

    private readonly SolidColorBrush NoteBrush;
    private readonly SolidColorBrush MissBrush;
    private readonly SolidColorBrush HitBrush;
    private readonly SolidColorBrush DraggedBrush;
    private readonly SolidColorBrush RushedBrush;

    private readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(250, 250, 250));

    private readonly SolidColorBrush SubdivisionBrush;

    private List<double> _userInputs = new();

    public NoteLaneControl()
    {
        SubdivisionBrush = FindResource("MaterialDesign.Brush.Primary.Light") as SolidColorBrush;
        
        NoteBrush = FindResource("MaterialDesign.Brush.Secondary.Light") as SolidColorBrush;
        MissBrush = FindResource("MaterialDesign.Brush.ValidationError") as SolidColorBrush;
        HitBrush = FindResource("MaterialDesign.Brush.Secondary.Dark") as SolidColorBrush;
        DraggedBrush = FindResource("MaterialDesign.Brush.Primary.Light") as SolidColorBrush;
        RushedBrush = new SolidColorBrush(Colors.Orange);

        NoteBrush.Freeze();
        //RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
    }

    private StateChangeEventArgs _lastStateChangeEventArgs;

    public NoteLaneControl(LaneContext lane, NoteHighwayControl owner)
     : this()
    {
        _lane = lane;
        _owner = owner;

        lane.StateChanged += (_, args) =>
        {
            try
            {
                _lastStateChangeEventArgs = args;
                if (args.CleanState)
                {
                    _userInputs.Clear();

                    foreach (var note in _lane.Notes)
                    {
                        note.State = NoteState.Pending;
                        note.HitOffsetMs = null;
                    }
                }

                Dispatcher.Invoke(InvalidateVisual);
            }
            catch { }

        };

        lane.InputReceived += (_, args) =>
        {
            try
            {
                var closestNote = _lane.Notes.MinBy(e => Math.Abs(e.StartTimeMs - args.TimeInMs));
                var hitOffset = args.TimeInMs - closestNote.StartTimeMs;

                var distance = Math.Abs(hitOffset);
                if (distance < ImperfectHitLimitMs)
                {
                    closestNote.HitOffsetMs = hitOffset;
                    closestNote.State = distance <= PerfectHitLimitMs
                        ? NoteState.Hit
                        : hitOffset > 0
                            ? NoteState.Dragged
                            : NoteState.Rushed;

                }
                else
                {
                    _userInputs.Add(args.TimeInMs);
                }

                Dispatcher.Invoke(InvalidateVisual);
            }
            catch { }

        };
    }


    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var isCleaning = _lastStateChangeEventArgs?.CleanState ?? false;
        _lastStateChangeEventArgs = null;

        if (isCleaning)
        {
            _userInputs.Clear();
        }

        var lane = _lane ?? DataContext as LaneContext;
        if (lane == null) return;

        var laneHeight = _lane == null ? Height : (Parent as Grid).ActualHeight / lane.Chunk.Lanes.Count;

        var margin = _lane != null
            ? 10
            : 1;

        var midLane = laneHeight / 2;
        var height = Math.Min(laneHeight - (margin * 2), 60);
        var y = (laneHeight - height) / 2;

        if (_lane != null)
        {
            dc.DrawLine(new Pen(SubdivisionBrush, 0.5), new Point(0, 0), new Point(ActualWidth, 0));
        }

        var pixelPerMs = _owner?.PixelPerMs ?? ActualWidth / lane.Chunk.Channel.Song.LengthMs;
        double cornerRadius = _lane == null ? 1 : 4;

        

        foreach (var note in lane.Notes)
        {
            var brush = _lane == null ? NoteBrush : note.State switch
            {
                NoteState.Pending => NoteBrush,
                NoteState.Hit => HitBrush,
                NoteState.Missed => MissBrush,
                NoteState.Dragged => DraggedBrush,
                NoteState.Rushed => RushedBrush
            };

            dc.DrawRoundedRectangle(
                brush: brush,
                pen: null,
                rectangle: new Rect(
                    x: (note.NoteRectStartMs + (note.HitOffsetMs ?? 0)) * pixelPerMs,
                    y: y,
                    width: note.NoteWidthMs * pixelPerMs,
                    height: height)
                , cornerRadius, cornerRadius);
        }

        if (_lane != null)
        {
            foreach (var userInput in _userInputs)
            {
                dc.DrawEllipse(
                    brush: MissBrush,
                    pen: null,
                    new Point(userInput * _owner.PixelPerMs, midLane),
                    10, 10);
            }
        }
    }


    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        InvalidateVisual();
    }
}
