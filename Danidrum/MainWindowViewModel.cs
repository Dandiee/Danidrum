using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.Windows.Media;
using Danidrum.AudioEngine;
using Danidrum.Context;
using Melanchall.DryWetMidi.Core;
using Microsoft.Win32;
using ChunkContext = Danidrum.Context.ChunkContext;

namespace Danidrum;

public partial class MainWindowViewModel : ObservableObject
{
    //private Playback _playback;
    private MultiTrackAudioEngine _multiPlayback;

    [ObservableProperty] private string _currentMidiFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ChunkContext _selectedChunk;

    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private DoubleCollection _measureStartTimesInMs;
    [ObservableProperty] private IReadOnlyList<string> _inputDevices;
    [ObservableProperty] private IReadOnlyList<OutputAudioDevice> _outputDevices;
    [ObservableProperty] private OutputAudioDevice _selectedOutputDevice;
    [ObservableProperty] private string _selectedInputDevice;
    [ObservableProperty] private bool _isReduced = true;
    [ObservableProperty] private double _visibleAreaMs;

    [ObservableProperty] private bool _isUserSeeking = false;

    [ObservableProperty] private double _currentTimeMs;
    [ObservableProperty] private double _rangeStartMs;
    [ObservableProperty] private double _rangeEndMs;


    [ObservableProperty] private SongContext _song;

    [ObservableProperty] private double _pixelPerMs = 0.3;
    [ObservableProperty] private double _visualLatencyInMs = 250;
    [ObservableProperty] private double _speed = 1;
    [ObservableProperty] private double _bpm = 0;

    private IOutputDevice _outputDevice;
    private InputDevice _inputDevice;
    [ObservableProperty] private IReadOnlyList<ChunkContext> _chunks;

    public MainWindowViewModel()
    {
        RefreshDevices();
    }

    partial void OnSpeedChanged(double value)
    {
        _multiPlayback.Speed = value;
        Bpm = Song.TempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute * value;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (value)
        {
            Song.Clean();

            if (_multiPlayback == null)
            {
                CreatePlayback();
            }

            _multiPlayback.Play();
        }
        else _multiPlayback.Stop();
    }

    partial void OnIsReducedChanged(bool value) => LoadSong(Song.FilePath);

    partial void OnRangeStartMsChanged(double value)
    {
        if (_multiPlayback != null)
        {
            _multiPlayback.PlaybackStart = new MetricTimeSpan(TimeSpan.FromMilliseconds(value));
        }
    }

    partial void OnRangeEndMsChanged(double value)
    {
        if (_multiPlayback != null)
        {
            _multiPlayback.PlaybackEnd = new MetricTimeSpan(TimeSpan.FromMilliseconds(value));
        }
    }

    private void LoadSong(string path)
    {
        IsPlaying = false;
        Song = new SongContext(path, IsReduced);
        Chunks = Song.Chunks.ToList();
        Bpm = Song.TempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute;
        MeasureStartTimesInMs = new DoubleCollection(Song.Measures.Select(m => m.StartTimeMs).ToList());
        SelectedChunk = Chunks.FirstOrDefault(e => e.Instrument.Id == 1024 && e.IsDrumTrack) ??
                        Chunks.FirstOrDefault(t => t.IsDrumTrack) ?? Chunks.FirstOrDefault();
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        IsLoading = false;

        if (SelectedOutputDevice != null)
        {
            CreatePlayback();
        }
    }

    private void CreatePlayback()
    {
        IsPlaying = false;

        if (_multiPlayback != null)
        {
            _multiPlayback.OnNotePlayed -= PlaybackOnNotesPlaybackFinished;
            _multiPlayback.OnRepeatStarted -= PlaybackOnRepeatStarted;
            //_multiPlayback.NoteCallback = null;
            _multiPlayback.Dispose();
            _multiPlayback = null;
        }

        _multiPlayback = new MultiTrackAudioEngine(Song, "GeneralUser-GS.sf2", SelectedOutputDevice);
        //_multiPlayback.NoteCallback = ChannelMuteFilter;
        _multiPlayback.OnNotePlayed += PlaybackOnNotesPlaybackFinished;
        _multiPlayback.Loop = true;
        _multiPlayback.OnRepeatStarted += PlaybackOnRepeatStarted;
    }

    private void PlaybackOnRepeatStarted(object? sender, EventArgs e)
    {
        Song.Clean();
    }

    [RelayCommand]
    private async Task Loaded()
    {
        RefreshDevices();
        LoadSong("test.mid");
    }

    partial void OnIsUserSeekingChanged(bool value)
    {
        if (!value)
        {
            _multiPlayback.MoveToTime(new MetricTimeSpan(TimeSpan.FromMilliseconds(CurrentTimeMs)));
        }
    }

    [RelayCommand]
    private void TogglePlayPause() => IsPlaying = !IsPlaying;

    [RelayCommand]
    private void Drop(DragEventArgs args)
    {
        if (args.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])args.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                var file = files.Select(e => new FileInfo(e)).FirstOrDefault(e => e.Exists && e.Extension.Equals(".mid"));
                if (file != null)
                {
                    LoadSong(file.FullName);
                }
            }
        }
    }

    [RelayCommand]
    private void Open()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select a MIDI File",
            Filter = "MIDI Files (*.mid, *.midi)|*.mid;*.midi|All Files (*.*)|*.*",
            FilterIndex = 1,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadSong(openFileDialog.FileName);
        }
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        InputDevices = InputDevice.GetAll().Select(e => e.Name).ToList();
        OutputDevices = Audio.GetOutputDevices();

        SelectedInputDevice = InputDevices.FirstOrDefault();
        SelectedOutputDevice = OutputDevices.FirstOrDefault(e => e.IsDefault);
    }

    [RelayCommand]
    private void EmulateSnareHit()
    {
        var snareLane = SelectedChunk.Lanes.FirstOrDefault(e => e.KitArticulation == KitArticulation.Snare);
        if (snareLane == null) return;

        snareLane.InputReceived?.Invoke(this, new InputArg(_currentTimeMs));
    }

    [RelayCommand]
    private void TrackChanged()
    {
        var currentTime = _multiPlayback?.GetCurrentTime();
        var isPlaying = IsPlaying;
        CreatePlayback();

        if (currentTime.HasValue)
        {
            IsUserSeeking = true;
            CurrentTimeMs = currentTime.Value;
            IsUserSeeking = false;
            //_multiPlayback.MoveToTime(new MetricTimeSpan(TimeSpan.FromMilliseconds(CurrentTimeMs)));
        }

        IsPlaying = isPlaying;
    }

    partial void OnSelectedInputDeviceChanged(string value)
    {
        if (_inputDevice != null)
        {
            _inputDevice.EventReceived -= OnMidiEvent;
            _inputDevice.Dispose();
            _inputDevice = null;
        }

        if (value != null)
        {
            _inputDevice = InputDevice.GetByName(value);
            _inputDevice.EventReceived += OnMidiEvent;
            _inputDevice.StartEventsListening();
        }
    }

    partial void OnSelectedOutputDeviceChanged(OutputAudioDevice value)
    {
        IsPlaying = false;

        if (_outputDevice != null)
        {
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        if (value != null && Song?.Midi != null)
        {
            CreatePlayback();
        }
    }

    private void PlaybackOnNotesPlaybackFinished(object? sender, NotePlayedArgs e)
    {
        foreach (var note in e.NoteEventArgs.Notes)
        {
            if (SelectedChunk != e.Chunk) continue;

            var lane = e.Chunk.LanesMapping[note.NoteNumber];
            var ctx = lane.NoteStartTimeMapping[note.Time];
            if (ctx.State == NoteState.Pending)
            {
                ctx.State = NoteState.Missed;
            }

            lane.StateChanged?.Invoke(this, new StateChangeEventArgs(false));
        }
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (_multiPlayback != null && !IsUserSeeking)
        {
            CurrentTimeMs = _multiPlayback.GetCurrentTime();
        }
    }

    private void OnMidiEvent(object sender, MidiEventReceivedEventArgs e)
    {
        if (!IsPlaying) return;

        if (e.Event is NoteOnEvent noteOn)
        {
            var articulation = Articulation.Td07NoteToArticulation[noteOn.NoteNumber];
            var kitArticulation = Articulation.ArticulationToKitArticulation[articulation];

            if (SelectedChunk.LanesMapping.TryGetValue((int)kitArticulation, out var lane))
            {
                lane.InputReceived?.Invoke(this, new InputArg(_currentTimeMs));
            }

            Debug.WriteLine($"{noteOn.NoteNumber}, {noteOn.Channel}, {noteOn.EventType}");
        }
    }

    public record InputArg(double TimeInMs);
}