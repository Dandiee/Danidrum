using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;

public class MeasureInfo
{
    public int MeasureIndex { get; set; }
    public long StartTick { get; set; }
    public long EndTick { get; set; }
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public Tempo Tempo { get; set; }
    public TimeSignature TimeSignature { get; set; }
}

public static class MeasureExtractor
{
    public static List<MeasureInfo> Extract(MidiFile midi)
    {
        var tempoMap = midi.GetTempoMap();
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

        var result = new List<MeasureInfo>();

        long maxTime = midi.GetTimedEvents().Last().Time;
        long currentTick = 0;
        int measure = 1;

        while (currentTick < maxTime)
        {
            var ts = GetSignatureAt(currentTick);
            var tempo = GetTempoAt(currentTick);

            // bar = numerator * (1 / denominator)
            var barLengthMusical = new MusicalTimeSpan(1, ts.Denominator) * ts.Numerator;

            long barLengthTicks = TimeConverter.ConvertFrom(barLengthMusical, tempoMap);

            long nextTick = currentTick + barLengthTicks;
            if (nextTick > maxTime)
                nextTick = maxTime;

            var startMs = TimeConverter.ConvertTo<MetricTimeSpan>(currentTick, tempoMap).TotalMicroseconds / 1000.0;
            var endMs = TimeConverter.ConvertTo<MetricTimeSpan>(nextTick, tempoMap).TotalMicroseconds / 1000.0;

            result.Add(new MeasureInfo
            {
                MeasureIndex = measure,
                StartTick = currentTick,
                EndTick = nextTick,
                StartTimeMs = startMs,
                EndTimeMs = endMs,
                Tempo = tempo,
                TimeSignature = ts
            });

            currentTick = nextTick;
            measure++;
        }

        return result;
    }
}
