using CommunityToolkit.Mvvm.ComponentModel;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Danidrum.Context;

public partial class ChunkContext : ObservableObject
{
    public SongContext Song { get; }
    public TrackChunk TrackChunk { get; }
    public IReadOnlyList<LaneContext> Lanes { get; }
    public bool IsDrumTrack { get; }
    public string Name { get; }
    public string InstrumentName { get; }

    [ObservableProperty] public Instrument _instrument;
    [ObservableProperty] public InstrumentCategory _instrumentCategory;
    [ObservableProperty] public bool _useDistortion;
    [ObservableProperty] public float _drive = 50f;
    [ObservableProperty] public float _gain = 0.075f;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private float _volume;

    partial void OnInstrumentCategoryChanged(InstrumentCategory value) => Instrument = value.Instruments.First();

    public Dictionary<int, LaneContext> LanesMapping;
    public List<TimedNoteEvent> Notes { get; }


    public ChunkContext(SongContext song, TrackChunk trackChunk, bool useReduction)
    {
        Song = song;
        TrackChunk = trackChunk;
        var instrumentId = trackChunk.Events.OfType<ProgramChangeEvent>().First().ProgramNumber;
        Instrument = Instrument.Mapping[instrumentId];
        InstrumentCategory = InstrumentCategory.Mapping[Instrument.Category];

        Name = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text ?? "Unknown Track";
        InstrumentName = string.Join(", ", trackChunk.Events.OfType<InstrumentNameEvent>().Select(e => e.Text));
        IsDrumTrack = Instrument.Id == 1024;

        _useDistortion = Instrument.Id == 29 || Instrument.Id == 30;

        Notes = GetTimedNoteEvents(trackChunk.Events.ToList());
        Volume = 1;

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