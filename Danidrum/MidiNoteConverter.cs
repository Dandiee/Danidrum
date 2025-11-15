namespace Danidrum;

public static class MidiNoteConverter
{
    // A mapping for General MIDI (GM) Drum notes (Channel 9/10)
    private static readonly Dictionary<int, string> GmDrumMap = new()
    {
        { 35, "Acoustic Bass Drum" },
        { 36, "Bass Drum 1" },
        { 37, "Side Stick" },
        { 38, "Acoustic Snare" },
        { 40, "Electric Snare" },
        { 41, "Low Floor Tom" },
        { 42, "Closed Hi-Hat" },
        { 43, "High Floor Tom" },
        { 44, "Pedal Hi-Hat" },
        { 45, "Low Tom" },
        { 46, "Open Hi-Hat" },
        { 48, "Hi-Mid Tom" },
        { 49, "Crash Cymbal 1" },
        { 51, "Ride Cymbal 1" },
        // ... add more as needed
    };

    private static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static string GetNoteName(int noteNumber, int channelId)
    {
        // If it's the drum channel, use the drum map
        if (channelId == 9 && GmDrumMap.TryGetValue(noteNumber, out var drumName))
        {
            return $"{NoteNames[noteNumber % 12]}{noteNumber / 12 - 1} ({drumName})";
        }

        // Otherwise, just use the standard note name
        return $"{NoteNames[noteNumber % 12]}{noteNumber / 12 - 1}";
    }
}