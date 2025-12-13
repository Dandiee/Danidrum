using CommunityToolkit.Mvvm.ComponentModel;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using static Danidrum.MainWindowViewModel;
using DryWetMidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum.Services;

public struct NoteKey : IEquatable<NoteKey>
{
    public int Channel { get; set; }
    public int NoteNumber { get; set; }
    public long Time { get; set; } // Absolute Time in Ticks (crucial for uniqueness)

    public bool Equals(NoteKey other) =>
        Channel == other.Channel &&
        NoteNumber == other.NoteNumber &&
        Time == other.Time;

    public override int GetHashCode() => HashCode.Combine(Channel, NoteNumber, Time);

    // Optional: Used for debugging
    public override string ToString() => $"C:{Channel}, N:{NoteNumber}, T:{Time}";
}


public class SongContext
{
    public DryWetMidiFile Midi { get; }
    public string FilePath { get; }
    public TempoMap TempoMap { get; }
    public IReadOnlyList<ChunkContext> Chunks { get; }
    public IReadOnlyList<MeasureContext> Measures { get; }
    public double LengthMs { get; }
    public bool IsReduced { get; }

    public IReadOnlyDictionary<TrackChunk, ChunkContext> TrackMapping { get; }

    public SongContext(string midiFilePath, bool useReduction)
    {
        FilePath = midiFilePath;
        Midi = DryWetMidiFile.Read(midiFilePath);
        IsReduced = useReduction;
        TempoMap = Midi.GetTempoMap();

        Chunks = Midi.Chunks.OfType<TrackChunk>()
            .Where(e => e.Events.Count > 100)
            .Select(e => new ChunkContext(this, e, false))
            .ToList();

        TrackMapping = Chunks.ToDictionary(e => e.TrackChunk);

        LengthMs = Midi.GetDuration<MetricTimeSpan>().TotalMilliseconds;
        Measures = Extract(TempoMap, Midi.GetDuration<MidiTimeSpan>()).ToList();
    }

    public void Clean()
    {
        foreach (var lane in Chunks.SelectMany(e => e.Lanes))
        {
            lane.StateChanged?.Invoke(this, new StateChangeEventArgs(true));
        }
    }

    private static List<MeasureContext> Extract(TempoMap tempoMap, long endTime)
    {
        var tempoChanges = tempoMap.GetTempoChanges().ToList();
        var signatureChanges = tempoMap.GetTimeSignatureChanges().ToList();

        TimeSignature GetSignatureAt(long ticks)
        {
            return signatureChanges
                .Where(c => c.Time <= ticks)
                .OrderByDescending(c => c.Time)
                .Select(c => c.Value)
                .FirstOrDefault() ?? TimeSignature.Default;
        }

        Tempo GetTempoAt(long ticks)
        {
            return tempoChanges
                .Where(c => c.Time <= ticks)
                .OrderByDescending(c => c.Time)
                .Select(c => c.Value)
                .FirstOrDefault() ?? Tempo.Default;
        }

        var result = new List<MeasureContext>();
        long currentTick = 0;
        int measure = 1;

        while (currentTick < endTime)
        {
            var signature = GetSignatureAt(currentTick);
            var tempo = GetTempoAt(currentTick);

            var barLengthMusical = new MusicalTimeSpan(1, signature.Denominator) * signature.Numerator;
            var barLengthTicks = TimeConverter.ConvertFrom(barLengthMusical, tempoMap);

            var nextTick = currentTick + barLengthTicks;
            if (nextTick > endTime)
                nextTick = endTime;

            var startMs = TimeConverter.ConvertTo<MetricTimeSpan>(currentTick, tempoMap).TotalMilliseconds;
            var endMs = TimeConverter.ConvertTo<MetricTimeSpan>(nextTick, tempoMap).TotalMilliseconds;

            result.Add(new MeasureContext
            {
                MeasureIndex = measure,
                StartTimeMs = startMs,
                EndTimeMs = endMs,
                Tempo = tempo,
                LengthMs = endMs - startMs,
                TimeSignature = signature
            });

            currentTick = nextTick;
            measure++;
        }

        return result;
    }
}

public partial class ChunkContext : ObservableObject
{
    public static readonly HashSet<string> DrumKeywords = new(["drum", "kit", "perc"], StringComparer.OrdinalIgnoreCase);

    public SongContext Song { get; }
    public TrackChunk TrackChunk { get; }
    public IReadOnlyList<LaneContext> Lanes { get; }

    public string Name { get; }
    public string InstrumentName { get; }
    public int InstrumentId { get; }
    public bool IsLikelyDrumTrack { get; }

    [ObservableProperty] private bool _isMuted = false;

    public Dictionary<int, LaneContext> LanesMapping;

    public List<TimedNoteEvent> Notes { get; }


    public ChunkContext(SongContext song, TrackChunk trackChunk, bool useReduction)
    {
        Song = song;
        TrackChunk = trackChunk;
        InstrumentId = trackChunk.Events.OfType<ProgramChangeEvent>().First().ProgramNumber;
        Name = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "Unknown Track";
        InstrumentName = string.Join(", ", trackChunk.Events.OfType<InstrumentNameEvent>().Select(e => e.Text));
        IsLikelyDrumTrack = InstrumentId == 1024;

        Notes = GetTimedNoteEvents(trackChunk.Events.ToList());

        var notesByNumbers = Notes
            .GroupBy(e => useReduction
                                        ? (int)Articulation.GetKitArticulation(e.NoteNumber)
                                        : e.NoteNumber);

        Lanes = notesByNumbers.Select(grp => new LaneContext(this, grp.Key, grp.ToList(), useReduction)).OrderBy(lane => lane.LaneId).ToList();

        LanesMapping = Lanes.ToDictionary(e => e.LaneId);
    }

    private List<TimedNoteEvent> GetTimedNoteEvents(List<MidiEvent> events)
    {
        var timedEvents = new List<TimedNoteEvent>();

        var time = 0L;
        var measureIndex = 0;

        var noteOns = new List<(int Index, TimedEvent On)>();
        for (var i = 0; i < events.Count; i++)
        {
            var midiEvent = events[i];
            time += midiEvent.DeltaTime;

            if (midiEvent is MarkerEvent marker && marker.Text.StartsWith("MEASURE_"))
            {
                measureIndex = int.Parse(marker.Text.Split('_')[1]);
            }
            else if (midiEvent is NoteOnEvent noteOn)
            {
                noteOns.Add(new(i, new TimedEvent(noteOn, time)));
            }
            else if (midiEvent is NoteOffEvent noteOff)
            {
                var pair = noteOns.First(e =>
                {
                    var on = (NoteOnEvent)e.On.Event;
                    return on.Channel == noteOff.Channel && on.NoteNumber == noteOff.NoteNumber;
                });

                noteOns.Remove(pair);
                timedEvents.Add(new TimedNoteEvent(measureIndex, pair.Index, pair.On, new TimedEvent(noteOff, time)));
            }
        }

        if (noteOns.Count > 0) throw new Exception();

        return timedEvents
            .OrderBy(e => e.MeasureIndex)
            .ThenBy(e => e.EventIndex)
            .ToList();
    }
}

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


public class StateChangeEventArgs(bool cleanState) : EventArgs
{
    public bool CleanState { get; } = cleanState;
}

public class LaneContext
{
    public const double PerfectWholeNoteWidthMs = 150;
    public const double MinimumNoteMarginMs = 15;

    public ChunkContext Chunk { get; }
    public int LaneId { get; }
    public string Name { get; }
    public IReadOnlyList<NoteContext> Notes { get; }
    public EventHandler<StateChangeEventArgs> StateChanged { get; set; }
    public EventHandler<InputArg> InputReceived { get; set; }
    public KitArticulation KitArticulation { get; set; }
    public IReadOnlyDictionary<long, NoteContext> NoteStartTimeMapping { get; }

    public LaneContext(ChunkContext chunk, int laneId, IReadOnlyList<TimedNoteEvent> notes, bool useReduction)
    {
        Chunk = chunk;
        LaneId = laneId;
        Name = useReduction
            ? Articulation.KitArticulationToName[(KitArticulation)LaneId]
            : Articulation.GetGmNoteName(LaneId, Chunk.InstrumentId);

        KitArticulation = (KitArticulation)LaneId;
        Notes = notes.Select(note => new NoteContext(this, note)).ToList();

        NoteStartTimeMapping = Notes
            .GroupBy(e => e.Note.Start)
            .ToDictionary(nc => nc.Key, nc => nc.First());

        SetNoteWidths();
    }

    private void SetNoteWidths()
    {
        const double idealHalfWidth = PerfectWholeNoteWidthMs / 2.0;

        for (var i = 0; i < Notes.Count; i++)
        {
            var note = Notes[i];

            var maxHalfWidth = idealHalfWidth * note.BeatFractionLength;

            if (i > 0)
            {
                note.Previous = Notes[i - 1];
                var distanceMs = note.StartTimeMs - note.Previous.StartTimeMs;
                var halfWidthFromPreviousConstraint = (distanceMs - MinimumNoteMarginMs) / 2.0;
                maxHalfWidth = Math.Min(maxHalfWidth, halfWidthFromPreviousConstraint);
            }

            if (i < Notes.Count - 1)
            {
                note.Next = Notes[i + 1];
                var distanceMs = note.Next.StartTimeMs - note.StartTimeMs;
                var halfWidthFromNextConstraint = (distanceMs - MinimumNoteMarginMs) / 2.0;
                maxHalfWidth = Math.Min(maxHalfWidth, halfWidthFromNextConstraint);
            }

            maxHalfWidth = Math.Max(0, maxHalfWidth);
            note.NoteWidthMs = 2.0 * maxHalfWidth;
            note.NoteRectStartMs = note.StartTimeMs - maxHalfWidth;
        }
    }
}

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

public class MeasureContext
{
    public int MeasureIndex { get; set; }
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public double LengthMs { get; set; }
    public Tempo Tempo { get; set; }
    public TimeSignature TimeSignature { get; set; }
}