using System.Collections.Generic;

namespace Danidrum.ViewModels;

public class BarViewModel
{
    public double X { get; set; }            // Canvas.Left
    public string TimeSignature { get; set; } // "4/4", "3/4", etc.

    // New: measure index (1-based) and a precomputed display text
    public int MeasureIndex { get; set; }
    public string DisplayText { get; set; }

    // X positions (absolute pixels) for subdivisions (beats) within the bar
    public List<double> SubdivisionPositions { get; } = new List<double>();
}