using CommunityToolkit.Mvvm.ComponentModel;
using Melanchall.DryWetMidi.Common;
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
    public IReadOnlyList<ChannelContext> Channels { get; }
    public IReadOnlyList<MeasureContext> Measures { get; }
    public double LengthMs { get; }
    public bool IsReduced { get; }

    public IReadOnlyDictionary<int, ChannelContext> ChannelsById { get; }

    public SongContext(string midiFilePath, bool useReduction)
    {
        FilePath = midiFilePath;
        Midi = DryWetMidiFile.Read(midiFilePath);
        IsReduced = useReduction;
        TempoMap = Midi.GetTempoMap();
        var channelGroups = Midi.GetTrackChunks().GroupBy(grp => grp.Events.GetNotes().First().Channel);
        Channels = channelGroups.Select(grp => new ChannelContext(this, grp, useReduction)).OrderBy(e => e.ChannelId).ToList();

        //Chunks = Midi.GetTrackChunks().Select(chunk => new ChunkContext(this, chunk)).OrderBy(e => e.ChannelId).ToList();
        LengthMs = Midi.GetDuration<MetricTimeSpan>().TotalMilliseconds;
        Measures = Extract(TempoMap, Midi.GetDuration<MidiTimeSpan>()).ToList();

        ChannelsById = Channels.ToDictionary(e => (int)e.ChannelId);
    }

    public void Clean()
    {
        foreach (var lane in Channels.SelectMany(e => e.Chunks).SelectMany(e => e.Lanes))
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

public class MeasureContext
{
    public int MeasureIndex { get; set; }
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public double LengthMs { get; set; }
    public Tempo Tempo { get; set; }
    public TimeSignature TimeSignature { get; set; }
}

public sealed class ChannelContext
{
    public TrackChunk TrackChunk { get; }
    public IReadOnlyList<ChunkContext> Chunks { get; }
    public SongContext Song { get; }
    public FourBitNumber ChannelId { get; }

    public IReadOnlyDictionary<int, List<LaneContext>> LanesByNote { get; }


    public ChannelContext(SongContext song, IGrouping<FourBitNumber, TrackChunk> channelGroup, bool useReduction)
    {
        Song = song;
        ChannelId = channelGroup.Key;
        Chunks = channelGroup.Select(chunk => new ChunkContext(this, chunk, useReduction)).ToList();
        LanesByNote = Chunks
            .SelectMany(chunk => chunk.Lanes)
            .GroupBy(e => e.LaneId)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.ToList());
    }
}

public partial class ChunkContext : ObservableObject
{
    public static readonly HashSet<string> DrumKeywords = new(["drum", "kit", "perc"], StringComparer.OrdinalIgnoreCase);

    public TrackChunk TrackChunk { get; }
    public ChannelContext Channel { get; }

    public IReadOnlyList<LaneContext> Lanes { get; }

    public string Name { get; }
    public string InstrumentName { get; }
    public int ChannelId { get; }
    public bool IsLikelyDrumTrack { get; }

    [ObservableProperty] private bool _isMuted = false;

    private readonly IReadOnlyDictionary<int, LaneContext> _lanesByNumbers;

    public ChunkContext(ChannelContext channel, TrackChunk trackChunk, bool useReduction)
    {
        Channel = channel;
        TrackChunk = trackChunk;
        var notes = trackChunk.GetNotes().ToList();
        ChannelId = notes[0].Channel;
        Name = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "Unknown Track";
        InstrumentName = string.Join(", ", trackChunk.Events.OfType<InstrumentNameEvent>().Select(e => e.Text));

        IsLikelyDrumTrack = DrumKeywords.Any(key => Name.Contains(key, StringComparison.OrdinalIgnoreCase)
                                                    || InstrumentName.Contains(key, StringComparison.OrdinalIgnoreCase));

        var notesByNumbers = notes.GroupBy(e => useReduction
            ? (int)Articulation.GetKitArticulation(e.NoteNumber)
            : e.NoteNumber);

        Lanes = notesByNumbers.Select(grp => new LaneContext(this, grp.Key, grp.ToList(), useReduction)).OrderBy(lane => lane.LaneId).ToList();
        _lanesByNumbers = Lanes.ToDictionary(e => e.LaneId);
    }

    public bool TryGetLane(int laneId, out LaneContext lane)
    {
        if (_lanesByNumbers.TryGetValue(laneId, out var cachedLane))
        {
            lane = cachedLane;
            return true;
        }

        lane = null;
        return false;
    }
}


public class StateChangeEventArgs(bool cleanState) : EventArgs
{
    public bool CleanState { get; } = cleanState;
}

public class LaneContext
{
    public const double PerfectNoteWithMs = 75;
    public const double MinimumNoteMarginMs = 15;

    public ChunkContext Chunk { get; }
    public int LaneId { get; }
    public string Name { get; }
    public IReadOnlyList<NoteContext> Notes { get; }
    public EventHandler<StateChangeEventArgs> StateChanged { get; set; }
    public EventHandler<InputArg> InputReceived { get; set; }
    public KitArticulation KitArticulation { get; set; }
    public IReadOnlyDictionary<long, List<NoteContext>> NotesByStartTimeTick { get; }

    public LaneContext(ChunkContext chunk, int laneId, IReadOnlyList<Note> notes, bool useReduction)
    {
        Chunk = chunk;
        LaneId = laneId;
        Name = useReduction
            ? Articulation.KitArticulationToName[(KitArticulation)LaneId]
            : Articulation.GetGmNoteName(LaneId, Chunk.ChannelId);
        KitArticulation = (KitArticulation)LaneId;
        Notes = notes.Select(note => new NoteContext(this, note)).ToList();

        NotesByStartTimeTick = Notes
            .GroupBy(e => e.Time)
            .ToDictionary(nc => nc.Key, nc => nc.ToList());

        SetNoteWidths();
    }

    private void SetNoteWidths()
    {
        const double idealHalfWidth = PerfectNoteWithMs / 2.0;

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
    public Note Note { get; }
    public LaneContext Lane { get; }
    public double StartTimeMs { get; }
    public double DurationMs { get; }
    public double BeatFractionLength { get; }
    public NoteContext? Previous { get; set; }
    public NoteContext? Next { get; set; }

    public NoteContext(LaneContext lane, Note note)
    {
        State = NoteState.Pending;

        Note = note;
        Lane = lane;

        var time = note.TimeAs<MetricTimeSpan>(lane.Chunk.Channel.Song.TempoMap);
        var length = note.LengthAs<MetricTimeSpan>(lane.Chunk.Channel.Song.TempoMap);

        var barBeatFraction = note.TimeAs<BarBeatFractionTimeSpan>(Lane.Chunk.Channel.Song.TempoMap);
        BeatFractionLength = barBeatFraction.Beats;

        StartTimeMs = time.TotalMilliseconds;
        DurationMs = length.TotalMilliseconds;

        Time = Note.Time;
    }

    public ITimedObject Clone() => Note.Clone();

    public long Time { get; set; }
}
