using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Danidrum.Services;

namespace Danidrum.UserControls;

public class NoteLaneControl : FrameworkElement
{
    private readonly LaneContext _lane;

    private readonly SolidColorBrush NoteBrush;
    private readonly SolidColorBrush MissedNoteBrush = new(Color.FromRgb(255, 0, 0)); // Material Blue
    private readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(250, 250, 250));

    public NoteLaneControl(LaneContext lane)
    {
        _lane = lane;

        NoteBrush = FindResource("MaterialDesign.Brush.Primary") as SolidColorBrush;
        NoteBrush.Freeze();

        lane.StateChanged += (_, _) => Dispatcher.Invoke(InvalidateVisual);

        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
    }

  
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var height = (Parent as Grid).ActualHeight / _lane.Chunk.Lanes.Count;

        foreach (var note in _lane.Notes)
        {
            dc.DrawRoundedRectangle(
                brush: (DataContext as MainWindowViewModel).CurrentSongPositionMs > note.StartTimeMs ? MissedNoteBrush : NoteBrush, 
                pen:null,
                rectangle: new Rect(
                    x: (note.StartTimeMs - note.DurationMs/2) * NoteHighwayControl.PixelPerMs + 5,
                    y: 5,
                    width: Math.Max(5, note.DurationMs * NoteHighwayControl.PixelPerMs - 10),
                    height: height - 5)
                , 5, 5);
        }
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        InvalidateVisual();
    }
}
