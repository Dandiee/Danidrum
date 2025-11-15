using CommunityToolkit.Mvvm.ComponentModel;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using DryWetMidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum.Services;
public class SongContext
{
    public DryWetMidiFile Midi { get; }
    public TempoMap TempoMap { get; }
    public IReadOnlyList<ChunkContext> Chunks { get; }
    public IReadOnlyList<MeasureContext> Measures { get; }
    public List<NoteContext> Notes { get; }
    public double LengthMs { get; }

    public SongContext(string midiFilePath)
    {
        Midi = DryWetMidiFile.Read(midiFilePath);
        TempoMap = Midi.GetTempoMap();
        Chunks = Midi.GetTrackChunks().Select(chunk => new ChunkContext(this, chunk)).OrderBy(e => e.ChannelId).ToList();
        Notes = Chunks.SelectMany(chk => chk.Lanes).SelectMany(lane => lane.Notes).OrderBy(e => e.TimedEvent.Time).ToList();
        LengthMs = Midi.GetDuration<MetricTimeSpan>().TotalMilliseconds;
        Measures = Extract(TempoMap, Midi.GetDuration<MidiTimeSpan>()).ToList();
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

public partial class ChunkContext : ObservableObject
{
    public static readonly HashSet<string> DrumKeywords = new(["drum", "kit", "perc"], StringComparer.OrdinalIgnoreCase);

    public TrackChunk TrackChunk { get; }
    public SongContext Song { get; }

    public IReadOnlyList<LaneContext> Lanes { get; }

    public string Name { get; }
    public string InstrumentName { get; }
    public int ChannelId { get; }
    public bool IsLikelyDrumTrack { get; }

    [ObservableProperty] private bool _isMuted = false;

    public ChunkContext(SongContext song, TrackChunk trackChunk)
    {
        Song = song;
        TrackChunk = trackChunk;
        var notes = trackChunk.GetNotes().ToList();
        ChannelId = notes[0].Channel;
        Name = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "Unknown Track";
        InstrumentName = string.Join(", ", trackChunk.Events.OfType<InstrumentNameEvent>().Select(e => e.Text));
        
        IsLikelyDrumTrack = DrumKeywords.Any(key => Name.Contains(key, StringComparison.OrdinalIgnoreCase) 
                                                    || InstrumentName.Contains(key, StringComparison.OrdinalIgnoreCase));

        var notesByNumbers = notes.GroupBy(e => e.NoteNumber);
        Lanes = notesByNumbers.Select(grp => new LaneContext(this, grp.Key, grp.ToList())).ToList();
    }
}

public class LaneContext
{
    public ChunkContext Chunk { get; }
    public SevenBitNumber NoteNumber { get; }
    public string Name { get; }
    public IReadOnlyList<NoteContext> Notes {get;}
    public EventHandler StateChanged { get; set; }

    public LaneContext(ChunkContext chunk, SevenBitNumber noteNumber, IReadOnlyList<Note> notes)
    {
        Chunk = chunk;
        NoteNumber = noteNumber;
        Name = MidiNoteConverter.GetNoteName(noteNumber, Chunk.ChannelId);
        Notes = notes.Select(note => new NoteContext(this, note)).ToList();
    }
}

public class NoteContext
{
    public Note Note { get; }
    public LaneContext Lane { get; }
    public TimedEvent TimedEvent { get; }
    public double StartTimeMs { get; }
    public double DurationMs { get; }

    public NoteContext(LaneContext lane, Note note)
    {
        Note = note;
        Lane = lane;

        TimedEvent = note.GetTimedNoteOnEvent();

        var time = note.TimeAs<MetricTimeSpan>(lane.Chunk.Song.TempoMap);
        var length = note.LengthAs<MetricTimeSpan>(lane.Chunk.Song.TempoMap);

        StartTimeMs = time.TotalMilliseconds;
        DurationMs = length.TotalMilliseconds;
    }
}
