namespace Danidrum.ViewModels;

public class BarViewModel
{
    public double X { get; set; }            // Canvas.Left
    public string TimeSignature { get; set; } // "4/4", "3/4", etc.

    // New: measure index (1-based) and a precomputed display text
    public int MeasureIndex { get; set; }
    public string DisplayText { get; set; }
}