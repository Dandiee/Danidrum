using CommunityToolkit.Mvvm.ComponentModel;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Security.Permissions;
using System.Threading.Channels;
using System.Windows.Forms;
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
    public TempoMap TempoMap { get; }
    public IReadOnlyList<ChannelContext> Channels { get; }
    public IReadOnlyList<MeasureContext> Measures { get; }
    public double LengthMs { get; }

    public IReadOnlyDictionary<string, List<NoteContext>> _lookupTable;

    public SongContext(string midiFilePath)
    {
        Midi = DryWetMidiFile.Read(midiFilePath);
        TempoMap = Midi.GetTempoMap();
        var channelGroups = Midi.GetTrackChunks().GroupBy(grp => grp.Events.GetNotes().First().Channel);
        Channels = channelGroups.Select(grp => new ChannelContext(this, grp)).ToList();

        //Chunks = Midi.GetTrackChunks().Select(chunk => new ChunkContext(this, chunk)).OrderBy(e => e.ChannelId).ToList();
        LengthMs = Midi.GetDuration<MetricTimeSpan>().TotalMilliseconds;
        Measures = Extract(TempoMap, Midi.GetDuration<MidiTimeSpan>()).ToList();

        _lookupTable = Channels
            .SelectMany(ch => ch.Chunks)
            .SelectMany(chk => chk.Lanes)
            .SelectMany(lane => lane.Notes)
            .GroupBy(grp => GetId(grp.Note))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.ToList());
    }

    private string GetId(Note note) => $"{note.Channel}{note.NoteNumber}{note.Time}{note.EndTime}{note.Length}{note.Octave}{note.Velocity}";

    public List<NoteContext> GetNoteContexts(Note note) => _lookupTable.TryGetValue(GetId(note), out var ctx) ? ctx : null;

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

public partial class ChannelContext : ObservableObject
{
    public TrackChunk TrackChunk { get; }
    public IReadOnlyList<ChunkContext> Chunks { get; }
    public SongContext Song { get; }
    public FourBitNumber ChannelId { get; }

    public ChannelContext(SongContext song, IGrouping<FourBitNumber, TrackChunk> channelGroup)
    {
        Song = song;
        ChannelId = channelGroup.Key;
        Chunks = channelGroup.Select(chunk => new ChunkContext(this, chunk)).ToList();
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

    private readonly IReadOnlyDictionary<SevenBitNumber, LaneContext> _lanesByNumbers;

    public ChunkContext(ChannelContext channel, TrackChunk trackChunk)
    {
        Channel = channel;
        TrackChunk = trackChunk;
        var notes = trackChunk.GetNotes().ToList();
        ChannelId = notes[0].Channel;
        Name = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "Unknown Track";
        InstrumentName = string.Join(", ", trackChunk.Events.OfType<InstrumentNameEvent>().Select(e => e.Text));
        
        IsLikelyDrumTrack = DrumKeywords.Any(key => Name.Contains(key, StringComparison.OrdinalIgnoreCase) 
                                                    || InstrumentName.Contains(key, StringComparison.OrdinalIgnoreCase));

        var notesByNumbers = notes.GroupBy(e => e.NoteNumber);
        Lanes = notesByNumbers.Select(grp => new LaneContext(this, grp.Key, grp.ToList())).ToList();
        _lanesByNumbers = Lanes.ToDictionary(e => e.NoteNumber);
    }

    public bool TryGetLane(SevenBitNumber noteNumber, out LaneContext lane)
    {
        if (_lanesByNumbers.TryGetValue(noteNumber, out var cachedLane))
        {
            lane = cachedLane;
            return true;
        }

        lane = null;
        return false;
    } 
}

public class LaneContext
{
    public ChunkContext Chunk { get; }
    public SevenBitNumber NoteNumber { get; }
    public string Name { get; }
    public IReadOnlyList<NoteContext> Notes {get;}
    public EventHandler StateChanged { get; set; }
    public EventHandler<InputArg> InputReceived { get; set; }

    public LaneContext(ChunkContext chunk, SevenBitNumber noteNumber, IReadOnlyList<Note> notes)
    {
        Chunk = chunk;
        NoteNumber = noteNumber;
        Name = ArticulationMappings.GetGmNoteName(noteNumber, Chunk.ChannelId);
        Notes = notes.Select(note => new NoteContext(this, note)).ToList();
    }
}

public class NoteContext : ITimedObject
{
    public Note Note { get; }
    public LaneContext Lane { get; }
    public double StartTimeMs { get; }
    public double DurationMs { get; }

    public NoteContext(LaneContext lane, Note note)
    {
        Note = note;
        Lane = lane;

        var time = note.TimeAs<MetricTimeSpan>(lane.Chunk.Channel.Song.TempoMap);
        var length = note.LengthAs<MetricTimeSpan>(lane.Chunk.Channel.Song.TempoMap);

        StartTimeMs = time.TotalMilliseconds;
        DurationMs = length.TotalMilliseconds;

        Time = Note.Time;
    }

    public ITimedObject Clone() => Note.Clone();

    public long Time { get; set; }
}
