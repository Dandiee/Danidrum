using CommunityToolkit.Mvvm.ComponentModel;
using Danidrum.ViewModels;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

using DryWetMidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum.Services;

public class TrackListService
{
    public static readonly HashSet<string> DrumKeywords =
        new(new[] { "drum", "kit", "perc" }, StringComparer.OrdinalIgnoreCase);

    // --- THIS METHOD IS REPLACED ---
    // public List<TrackInfo> GetAllTrackInfo(DryWetMidiFile midiFile)

    // --- WITH THIS NEW METHOD ---
    public List<TrackInfo> GetAllTrackInfo(DryWetMidiFile midiFile)
    {
        var allTracks = new List<TrackInfo>();
        var rawTracks = midiFile.GetTrackChunks()
            .Select(chunk => new
            {
                Chunk = chunk,
                Notes = chunk.GetNotes(),
                TrackName = chunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text,
                InstrumentName = string.Join(", ", chunk.Events.OfType<InstrumentNameEvent>().Select(e => e.Text))
            })
            .Where(t => t.Notes.Any())
            .ToList();

        foreach (var rawTrack in rawTracks)
        {
            var channelId = rawTrack.Notes.First().Channel;
            var trackInfo = new TrackInfo(
                rawTrack.TrackName ?? $"Track (Ch {channelId + 1})",
                rawTrack.InstrumentName,
                rawTrack.Notes.Count,
                channelId,
                rawTrack.Chunk
            );

            trackInfo.IsLikelyDrumTrack = DrumKeywords.Any(key =>
                (rawTrack.TrackName != null && rawTrack.TrackName.Contains(key, StringComparison.OrdinalIgnoreCase)) ||
                (rawTrack.InstrumentName.Contains(key, StringComparison.OrdinalIgnoreCase))
            );

            allTracks.Add(trackInfo);
        }

        return allTracks;
    }
}

public partial class TrackInfo : ObservableObject
{
    // No _parent reference
    [ObservableProperty] private string _trackName;
    [ObservableProperty] private string _instrumentName;
    [ObservableProperty] private int _noteCount;
    [ObservableProperty] private int _channelId;
    [ObservableProperty] private TrackChunk _chunk;
    [ObservableProperty] private bool _isLikelyDrumTrack;

    [ObservableProperty]
    private bool _isMuted = false;

    // Back to a simple constructor
    public TrackInfo(string trackName, string instrumentName, int noteCount, int channelId, TrackChunk chunk)
    {
        _trackName = trackName;
        _instrumentName = instrumentName;
        _noteCount = noteCount;
        _channelId = channelId;
        _chunk = chunk;
    }
}