using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Danidrum.Context;

public class SongContext
{
    public MidiFile Midi { get; }
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
        Midi = MidiFile.Read(midiFilePath);
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