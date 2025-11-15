using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Danidrum.Services;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.Windows.Media;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace Danidrum;

public partial class MainWindowViewModel : ObservableObject
{
    private Playback _playback;

    [ObservableProperty] private string _currentMidiFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ChunkContext _selectedChunk;
    [ObservableProperty] private double _currentSongPositionMs;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private DoubleCollection _measureStartTimesInMs;
    [ObservableProperty] private IReadOnlyList<string> _inputDevices;
    [ObservableProperty] private IReadOnlyList<string> _outputDevices;
    [ObservableProperty] private string _selectedOutputDevice;
    [ObservableProperty] private string _selectedInputDevice;
    [ObservableProperty] private bool _isReduced;


    [ObservableProperty] private SongContext _song;

    [ObservableProperty] private double _pixelPerMs = 0.3;
    [ObservableProperty] private double _visualLatencyInMs = 250;
    [ObservableProperty] private double _speed = 1;
    [ObservableProperty] private double _bpm = 0;

    private OutputDevice _outputDevice;
    private InputDevice _inputDevice;
    [ObservableProperty] private IReadOnlyList<ChunkContext> _chunks;

    private HashSet<int> _mutedChannels = new();

    private bool _isUserSeeking = false;


    partial void OnSpeedChanged(double value)
    {
        _playback.Speed = value;
        Bpm = Song.TempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute * value;
    }

    partial void OnIsReducedChanged(bool value)
    {
        if (IsPlaying)
        {
            StopPlayback();
        }
        LoadSong();
    }

    private void LoadSong()
    {
        Song = new SongContext("Tool.mid", IsReduced);
        Chunks = Song.Channels.SelectMany(e => e.Chunks).ToList();
        Bpm = Song.TempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute;
        MeasureStartTimesInMs = new DoubleCollection(Song.Measures.Select(m => m.StartTimeMs).ToList());
        SelectedChunk = Chunks.SingleOrDefault(e => e.ChannelId == 9 && e.IsLikelyDrumTrack) ??
                        Chunks.FirstOrDefault(t => t.IsLikelyDrumTrack) ?? Chunks.FirstOrDefault();
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        IsLoading = false;

        if (_playback != null)
        {
            _playback.NotesPlaybackFinished -= PlaybackOnNotesPlaybackFinished;
            _playback.NoteCallback = null;
            _playback.Dispose();
            _playback = null;
        }

        _playback = Song.Midi.GetPlayback(_outputDevice);
        _playback.NoteCallback = NoteCallback;
        _playback.NotesPlaybackFinished += PlaybackOnNotesPlaybackFinished;
    }

    [RelayCommand]
    private async Task Loaded()
    {
        InputDevices = InputDevice.GetAll().Select(e => e.Name).ToList();
        OutputDevices = OutputDevice.GetAll().Select(e => e.Name).ToList();

        SelectedInputDevice = InputDevices.FirstOrDefault();
        SelectedOutputDevice = OutputDevices.FirstOrDefault();

        if (SelectedInputDevice != null)
        {
            _inputDevice = InputDevice.GetByName(SelectedInputDevice);
            _inputDevice.EventReceived += OnMidiEvent;
            _inputDevice.StartEventsListening();
        }

        _outputDevice = OutputDevice.GetByName(SelectedOutputDevice);

        if (SelectedOutputDevice == null)
        {
            MessageBox.Show("No output device :(");
            Application.Current.Shutdown();
        }

        LoadSong();
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
            if (ctx != null && ctx.Count == 1)
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
        if (_playback != null && !_isUserSeeking)
        {
            CurrentSongPositionMs = _playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
        }
    }

    [RelayCommand]
    private void StartPlayback()
    {
        
        
        _playback.Start();
        
        IsPlaying = true;
    }

    private void OnMidiEvent(object sender, MidiEventReceivedEventArgs e)
    {
        if (e.Event is NoteOnEvent noteOn)
        {
            var laneId = Song.IsReduced
                ? (int)Articulation.ArticulationToKitArticulation[Articulation.GmNoteToArticulation[noteOn.NoteNumber]]
                : noteOn.NoteNumber;

            if (SelectedChunk.TryGetLane(laneId, out var lane))
            {
                lane.InputReceived?.Invoke(this, new InputArg(noteOn.NoteNumber, _currentSongPositionMs));
            }

            Debug.WriteLine($"{noteOn.NoteNumber}, {noteOn.Channel}, {noteOn.EventType}");
        }
    }

    public record InputArg(SevenBitNumber NoteNumber, double TimeInMs);


    [RelayCommand]
    private void PausePlayback()
    {
        _playback.Stop();
        IsPlaying = false;
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (IsPlaying)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    [RelayCommand] // This now creates an IAsyncRelayCommand
    private void StopPlayback()
    {
        _playback.Stop();
        IsPlaying = false;
    }

    [RelayCommand]
    private void StartSeeking()
    {
        _isUserSeeking = true;
        _playback.Stop();
    }

    [RelayCommand]
    private async Task StopSeeking()
    {
        _playback.Start();
        _playback.MoveToTime(new MetricTimeSpan(TimeSpan.FromMilliseconds(CurrentSongPositionMs)));
        _isUserSeeking = false;
    }
}