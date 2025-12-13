namespace Danidrum.Context;

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
    public EventHandler<MainWindowViewModel.InputArg> InputReceived { get; set; }
    public KitArticulation KitArticulation { get; set; }
    public IReadOnlyDictionary<long, NoteContext> NoteStartTimeMapping { get; }

    public LaneContext(ChunkContext chunk, int laneId, IReadOnlyList<TimedNoteEvent> notes, bool useReduction)
    {
        Chunk = chunk;
        LaneId = laneId;
        Name = useReduction
            ? Articulation.KitArticulationToName[(KitArticulation)LaneId]
            : Articulation.GetGmNoteName(LaneId, Chunk.Instrument.Id);

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