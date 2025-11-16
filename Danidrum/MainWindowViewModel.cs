using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Danidrum.Services;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.Windows.Media;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Microsoft.Win32;
using NAudio.Wave.Asio;

namespace Danidrum;

public partial class MainWindowViewModel : ObservableObject
{
    private Playback _playback;

    [ObservableProperty] private string _currentMidiFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ChunkContext _selectedChunk;
    
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private DoubleCollection _measureStartTimesInMs;
    [ObservableProperty] private IReadOnlyList<string> _inputDevices;
    [ObservableProperty] private IReadOnlyList<string> _outputDevices;
    [ObservableProperty] private string _selectedOutputDevice;
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

    private HashSet<int> _mutedChannels = new();

    public MainWindowViewModel()
    {
        RefreshDevices();
    }

    partial void OnSpeedChanged(double value)
    {
        _playback.Speed = value;
        Bpm = Song.TempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute * value;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (value)
        {
            foreach (var lane in Song.Channels.SelectMany(e => e.Chunks).SelectMany(e => e.Lanes))
            {
                lane.StateChanged?.Invoke(this, new StateChangeEventArgs(true));
            }

            _playback.Start();
        }
        else _playback.Stop();
    }

    partial void OnIsReducedChanged(bool value) => LoadSong(Song.FilePath);

    partial void OnRangeStartMsChanged(double value)
    {
        if (_playback != null)
        {
            _playback.PlaybackStart = new MetricTimeSpan(TimeSpan.FromMilliseconds(value));
        }
    }

    partial void OnRangeEndMsChanged(double value)
    {
        if (_playback != null)
        {
            _playback.PlaybackEnd = new MetricTimeSpan(TimeSpan.FromMilliseconds(value));
        }
    }

    private void LoadSong(string path)
    {
        IsPlaying = false;
        Song = new SongContext(path, IsReduced);
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
        _playback.NoteCallback = ChannelMuteFilter;
        _playback.NotesPlaybackFinished += PlaybackOnNotesPlaybackFinished;
        _playback.Loop = true;
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
            _playback.MoveToTime(new MetricTimeSpan(TimeSpan.FromMilliseconds(CurrentTimeMs)));
        }
    }

    private NotePlaybackData ChannelMuteFilter(NotePlaybackData rawNoteData, long rawTime, long rawLength, TimeSpan playbackTime) => _mutedChannels.Contains(rawNoteData.Channel) ? null : rawNoteData;

    [RelayCommand]
    private void MuteStateChanged(ChunkContext chunk)
    {
        foreach (var chk in chunk.Channel.Chunks)
        {
            chk.IsMuted = chunk.IsMuted;
        }

        _mutedChannels = Song.Channels.SelectMany(e => e.Chunks).Where(e => e.IsMuted).Select(e => e.ChannelId).ToHashSet();
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
        OutputDevices = OutputDevice.GetAll().Select(e => e.Name).ToList();

        SelectedInputDevice = InputDevices.FirstOrDefault();
        SelectedOutputDevice = OutputDevices.FirstOrDefault();
    }

    [RelayCommand]
    private void EmulateSnareHit()
    {
        var snareLane = SelectedChunk.Lanes.FirstOrDefault(e => e.KitArticulation == KitArticulation.Snare);
        if (snareLane == null) return;

        snareLane.InputReceived?.Invoke(this, new InputArg(_currentTimeMs));
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

    partial void OnSelectedOutputDeviceChanged(string value)
    {
        IsPlaying = false;

        if (_outputDevice != null)
        {
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        if (value != null)
        {
            //_outputDevice = OutputDevice.GetByName(value);

            //_outputDevice = new AsioPolyphonicSynthDevice(AsioDriver.GetAsioDriverNames()[0]);

            string soundFontPath = "GeneralUser-GS.sf2";
            string asioDriverName = AsioDriver.GetAsioDriverNames()[0];

            // Use this as your OutputDevice for DryWetMidi's Playback
            _outputDevice = new AsioSoundFontSynthDevice(asioDriverName, soundFontPath);
            if (_playback != null)
            {
                _playback.OutputDevice = _outputDevice;
            }
        }
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
                    ctx[0].Lane.StateChanged?.Invoke(this, new StateChangeEventArgs(false));
                }
            }
        }
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (_playback != null && !IsUserSeeking)
        {
            CurrentTimeMs = _playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
        }
    }

    private void OnMidiEvent(object sender, MidiEventReceivedEventArgs e)
    {
        if (e.Event is NoteOnEvent noteOn)
        {
            var articulation = Articulation.Td07NoteToArticulation[noteOn.NoteNumber];
            var kitArticulation = Articulation.ArticulationToKitArticulation[articulation];

            if (SelectedChunk.TryGetLane((int)kitArticulation, out var lane))
            {
                lane.InputReceived?.Invoke(this, new InputArg(_currentTimeMs));
            }

            Debug.WriteLine($"{noteOn.NoteNumber}, {noteOn.Channel}, {noteOn.EventType}");
        }
    }

    public record InputArg(double TimeInMs);
}