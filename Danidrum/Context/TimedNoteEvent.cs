using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Danidrum.Context;

public sealed class TimedNoteEvent
{
    public int MeasureIndex { get; }
    public int EventIndex { get; }

    public long Start { get; }
    public long End { get; }
    public long Duration { get; }

    public int NoteNumber { get; }

    public NoteOnEvent On { get; }
    public NoteOffEvent Off { get; }

    public TimedNoteEvent(int measureIndex, int eventIndex, TimedEvent on, TimedEvent off)
    {
        MeasureIndex = measureIndex;
        EventIndex = eventIndex;
        Start = on.Time;
        End = off.Time;
        Duration = End - Start;
        On = (NoteOnEvent)on.Event;
        Off = (NoteOffEvent)off.Event;
        NoteNumber = On.NoteNumber;
    }
}