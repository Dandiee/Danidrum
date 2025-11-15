using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Danidrum.ViewModels;

namespace Danidrum.UserControls;

public class NoteLaneControl : FrameworkElement
{
    private static readonly SolidColorBrush NoteBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Material Blue
    private static readonly SolidColorBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(250, 250, 250));

    static NoteLaneControl()
    {
        NoteBrush.Freeze();
        BackgroundBrush.Freeze();
    }

    public NoteLaneControl()
    {
        // Improve rendering quality for rectangles
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
    }

    #region Dependency Properties

    public static readonly DependencyProperty NotesProperty = DependencyProperty.Register(
    nameof(Notes), typeof(IEnumerable), typeof(NoteLaneControl),
    new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnNotesChanged));

    public IEnumerable Notes
    {
        get => (IEnumerable)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NoteLaneControl)d;
        control.UnsubscribeFromCollection(e.OldValue as INotifyCollectionChanged);
        control.SubscribeToCollection(e.NewValue as INotifyCollectionChanged);
        control.InvalidateVisual();
    }

    public static readonly DependencyProperty NoteHeightProperty = DependencyProperty.Register(
    nameof(NoteHeight), typeof(double), typeof(NoteLaneControl),
    new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsRender, OnSimplePropertyChanged));

    public double NoteHeight
    {
        get => (double)GetValue(NoteHeightProperty);
        set => SetValue(NoteHeightProperty, value);
    }

    private static void OnSimplePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((NoteLaneControl)d).InvalidateVisual();
    }

    #endregion

    private void SubscribeToCollection(INotifyCollectionChanged? coll)
    {
        if (coll != null)
            coll.CollectionChanged += Notes_CollectionChanged;
    }

    private void UnsubscribeFromCollection(INotifyCollectionChanged? coll)
    {
        if (coll != null)
            coll.CollectionChanged -= Notes_CollectionChanged;
    }

    private void Notes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Minimal handling: redraw everything on collection changes
        Application.Current?.Dispatcher?.InvokeAsync(InvalidateVisual);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Let the parent size the control (usually the Canvas sets Width to song width and Height to lane height)
        return base.MeasureOverride(availableSize);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Draw background for the lane
        //dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (Notes == null) return;

        // Center notes vertically
        double y = Math.Max(0.0, (ActualHeight - NoteHeight) / 2.0);

        // Simple draw loop; the Notes collection contains NoteViewModel instances
        foreach (var item in Notes)
        {
            if (item is NoteViewModel note)
            {
                double x = note.CanvasLeft;
                double w = Math.Max(1.0, note.CanvasWidth);

                // Optionally do simple culling to avoid drawing rectangles far outside the visible area.
                if (x + w < 0 || x > ActualWidth + 1000) // small guard zone
                    continue;

                var rect = new Rect(x, y, w, NoteHeight);
                dc.DrawRoundedRectangle(NoteBrush, null, rect, Math.Min(5, NoteHeight / 2.0), Math.Min(5, NoteHeight / 2.0));
            }
        }
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        InvalidateVisual();
    }
}
