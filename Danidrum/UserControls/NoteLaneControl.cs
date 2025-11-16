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
    private readonly SolidColorBrush MissedNoteBrush = new(Color.FromRgb(255, 0, 0)); // Material Blue
    private readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(250, 250, 250));

    private readonly SolidColorBrush SubdivisionBrush;

    private List<double> _userInputs = new();

    public NoteLaneControl(LaneContext lane, NoteHighwayControl owner)
    {
        _lane = lane;
        _owner = owner;

        SubdivisionBrush = FindResource("MaterialDesign.Brush.Primary.Light") as SolidColorBrush;

        NoteBrush = FindResource("MaterialDesign.Brush.Secondary") as SolidColorBrush;
        NoteBrush.Freeze();

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

        //RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        
        SnapsToDevicePixels = true;
    }


    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var laneHeight = (Parent as Grid).ActualHeight / _lane.Chunk.Lanes.Count;

        var height = Math.Min(laneHeight - 10, 60);
        var y = (laneHeight - height) / 2;

        dc.DrawLine(new Pen(SubdivisionBrush, 0.8), new Point(0, 0), new Point(ActualWidth, 0));

        foreach (var note in _lane.Notes)
        {
            dc.DrawRoundedRectangle(
                brush: (DataContext as MainWindowViewModel).CurrentSongPositionMs > note.StartTimeMs ? MissedNoteBrush : NoteBrush, 
                pen:null,
                rectangle: new Rect(
                    x: (note.StartTimeMs - note.DurationMs/2) * _owner.PixelPerMs + 5,
                    y: y,
                    width: Math.Max(5, note.DurationMs * _owner.PixelPerMs - 10),
                    height: height)
                , 5, 5);
        }

        foreach (var userInput in _userInputs)
        {
            dc.DrawRoundedRectangle(
                brush: MissedNoteBrush,
                pen: null,
                rectangle: new Rect(
                    x: userInput * _owner.PixelPerMs,
                    y: y,
                    width: 10,
                    height: height),
                radiusX: 5,
                radiusY: 5);
        }
    }


    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        InvalidateVisual();
    }
}
