using Melanchall.DryWetMidi.Interaction;

namespace Danidrum.Context;

public enum NoteState
{
    Pending,
    Hit,
    Rushed,
    Dragged,
    Missed
}

public class NoteContext : ITimedObject
{
    public NoteState State { get; set; }
    public double? HitOffsetMs { get; set; }
    public double NoteWidthMs { get; set; }
    public double NoteRectStartMs { get; set; }
    public TimedNoteEvent Note { get; }
    public LaneContext Lane { get; }
    public double StartTimeMs { get; }
    public double DurationMs { get; }
    public double BeatFractionLength { get; }
    public NoteContext? Previous { get; set; }
    public NoteContext? Next { get; set; }

    public NoteContext(LaneContext lane, TimedNoteEvent note)
    {
        State = NoteState.Pending;

        Note = note;
        Lane = lane;

        var time = TimeConverter.ConvertTo<MetricTimeSpan>(note.Start, Lane.Chunk.Song.TempoMap);
        var length = TimeConverter.ConvertTo<MetricTimeSpan>(note.Duration, Lane.Chunk.Song.TempoMap);

        var barBeatFraction = TimeConverter.ConvertTo<BarBeatFractionTimeSpan>(length, Lane.Chunk.Song.TempoMap);
        BeatFractionLength = barBeatFraction.Beats;

        StartTimeMs = time.TotalMilliseconds;
        DurationMs = length.TotalMilliseconds;

        Time = Note.Start;
    }

    public ITimedObject Clone() => new NoteContext(null, null);

    public long Time { get; set; }
}