using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Danidrum.Services;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace Danidrum;

public partial class MainWindowViewModel : ObservableObject
{
    private Playback _playback;

    [ObservableProperty] private string _currentMidiFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ChunkContext _selectedChunk;
    [ObservableProperty] private double _currentSongPositionMs;
    [ObservableProperty] private bool _isPlaying;


    [ObservableProperty] private SongContext _song;

    [ObservableProperty] private double _pixelPerMs = 0.3;
    [ObservableProperty] private double _visualLatencyInMs = 250;

    private OutputDevice _outputDevice;
    [ObservableProperty] private IReadOnlyList<ChunkContext> _chunks;

    private HashSet<int> _mutedChannels = new();

    private bool _isUserSeeking = false;

    private void OnPlaybackPositionChanged(long currentTimeMs)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (!_isUserSeeking)
            {
                CurrentSongPositionMs = currentTimeMs;
            }
        });
    }

    [RelayCommand]
    private async Task Loaded()
    {
        IsLoading = true;
        Song = new SongContext("Tool.mid");
        Chunks = Song.Channels.SelectMany(e => e.Chunks).ToList();
        SelectedChunk = Chunks.FirstOrDefault(t => t.IsLikelyDrumTrack) ?? Chunks.FirstOrDefault();
        
        _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");
        _playback = Song.Midi.GetPlayback(_outputDevice);
        _playback.NoteCallback = NoteCallback;
        _playback.NotesPlaybackFinished += PlaybackOnNotesPlaybackFinished;
        
        System.Windows.Media.CompositionTarget.Rendering += CompositionTarget_Rendering;
        IsLoading = false;

        _playback.Start();
    }

    private NotePlaybackData NoteCallback(NotePlaybackData rawNoteData, long rawTime, long rawLength, TimeSpan playbackTime)
    {
        if (_mutedChannels.Contains(rawNoteData.Channel))
        {
            return null;
        }
        return rawNoteData;
    }

    [RelayCommand]
    private void MuteStateChanged(ChunkContext chunk)
    {
        foreach (var chk in chunk.Channel.Chunks)
        {
            chk.IsMuted = chunk.IsMuted;
        }

        _mutedChannels = Song.Channels.SelectMany(e => e.Chunks).Where(e => e.IsMuted).Select(e => e.ChannelId).ToHashSet();
    }

    private void PlaybackOnNotesPlaybackFinished(object? sender, NotesEventArgs e)
    {
        foreach (var note in e.Notes)
        {
            var ctx = Song.GetNoteContexts(note);
            if (ctx.Count == 1)
            {
                if (SelectedChunk.ChannelId == ctx[0].Lane.Chunk.ChannelId)
                {
                    ctx[0].Lane.StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        CurrentSongPositionMs = _playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
        
    }

    [RelayCommand]
    private void StartPlayback()
    {
        _playback.Start();
    }


    [RelayCommand]
    private async Task PausePlayback()
    {
        _playback.Stop();
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (IsPlaying)
        {
            PausePlaybackCommand.Execute(null);
        }
        else
        {
            StartPlaybackCommand.Execute(null);
        }
    }

    [RelayCommand] // This now creates an IAsyncRelayCommand
    private async Task StopPlayback()
    {
        _playback.Stop();
    }

    [RelayCommand]
    private void StartSeeking() => _isUserSeeking = true;

    [RelayCommand]
    private async Task StopSeeking()
    {
        //_playbackService.SeekTo((long)CurrentSongPositionMs);
        _isUserSeeking = false;
    }
}