using System.CodeDom;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Danidrum.Services;

namespace Danidrum.UserControls;

public class NoteLaneControl : FrameworkElement
{
    private readonly LaneContext _lane;
    private readonly NoteHighwayControl _owner;

    private readonly SolidColorBrush NoteBrush;
    private readonly SolidColorBrush MissedNoteBrush;
    private readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(250, 250, 250));

    private readonly SolidColorBrush SubdivisionBrush;

    private List<double> _userInputs = new();

    public NoteLaneControl()
    {
        SubdivisionBrush = FindResource("MaterialDesign.Brush.Primary.Light") as SolidColorBrush;
        NoteBrush = FindResource("MaterialDesign.Brush.Secondary.Light") as SolidColorBrush;
        MissedNoteBrush = FindResource("MaterialDesign.Brush.ValidationError") as SolidColorBrush;
        NoteBrush.Freeze();
        //RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
    }

    public NoteLaneControl(LaneContext lane, NoteHighwayControl owner)
     : this()
    {
        _lane = lane;
        _owner = owner;

        lane.StateChanged += (_, _) =>
        {
            try
            {
                Dispatcher.Invoke(InvalidateVisual);
            }
            catch { }

        };

        lane.InputReceived += (_, args) =>
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _userInputs.Add(args.TimeInMs);
                    InvalidateVisual();
                });
            }
            catch { }

        };
    }


    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var lane = _lane ?? DataContext as LaneContext;
        if (lane == null) return;

        var laneHeight = _lane == null ? Height : (Parent as Grid).ActualHeight / lane.Chunk.Lanes.Count;

        var margin = _lane != null
            ? 10
            : 1;

        var height = Math.Min(laneHeight - (margin * 2), 60);
        var y = (laneHeight - height) / 2;

        if (_lane != null)
        {
            dc.DrawLine(new Pen(SubdivisionBrush, 0.8), new Point(0, 0), new Point(ActualWidth, 0));
        }

        var pixelPerMs = _owner?.PixelPerMs ?? ActualWidth / lane.Chunk.Channel.Song.LengthMs;
        double cornerRadius = _lane == null ? 1 : 4;
        const double normaNoteDuration = 100;

        foreach (var note in lane.Notes)
        {
            var noteSize = 50;

            dc.DrawRoundedRectangle(
                brush: _lane == null
                    ? NoteBrush
                    : (DataContext as MainWindowViewModel).CurrentTimeMs > note.StartTimeMs ? MissedNoteBrush : NoteBrush,
                pen: null,
                rectangle: new Rect(
                    x: note.NoteRectStartMs * pixelPerMs,
                    y: y,
                    width: note.NoteWidthMs * pixelPerMs,
                    height: height)
                , cornerRadius, cornerRadius);
        }

        if (_lane != null)
        {
            foreach (var userInput in _userInputs)
            {
                dc.DrawRoundedRectangle(
                    brush: MissedNoteBrush,
                    pen: null,
                    rectangle: new Rect(
                        x: userInput * _owner.PixelPerMs,
                        y: y,
                        width: margin * 2,
                        height: height),
                    radiusX: cornerRadius,
                    radiusY: cornerRadius);
            }
        }
    }


    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        InvalidateVisual();
    }
}
