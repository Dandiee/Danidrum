using Melanchall.DryWetMidi.Interaction;

namespace Danidrum.Context;

public class MeasureContext
{
    public int MeasureIndex { get; set; }
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public double LengthMs { get; set; }
    public Tempo Tempo { get; set; }
    public TimeSignature TimeSignature { get; set; }
}