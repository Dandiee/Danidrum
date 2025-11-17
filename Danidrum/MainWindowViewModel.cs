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
using NAudio.CoreAudioApi;
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
            Song.Clean();

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
        _mutedChannels.Clear();
        Song = new SongContext(path, IsReduced);
        Chunks = Song.Channels.SelectMany(e => e.Chunks).ToList();
        Bpm = Song.TempoMap.GetTempoAtTime(new MetricTimeSpan(0)).BeatsPerMinute;
        MeasureStartTimesInMs = new DoubleCollection(Song.Measures.Select(m => m.StartTimeMs).ToList());
        SelectedChunk = Chunks.FirstOrDefault(e => e.ChannelId == 9 && e.IsLikelyDrumTrack) ??
                        Chunks.FirstOrDefault(t => t.IsLikelyDrumTrack) ?? Chunks.FirstOrDefault();
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        IsLoading = false;

        if (_playback != null)
        {
            _playback.NotesPlaybackFinished -= PlaybackOnNotesPlaybackFinished;
            _playback.RepeatStarted -= PlaybackOnRepeatStarted;
            _playback.NoteCallback = null;
            _playback.Dispose();
            _playback = null;
        }

        _playback = Song.Midi.GetPlayback(_outputDevice);
        _playback.NoteCallback = ChannelMuteFilter;
        _playback.NotesPlaybackFinished += PlaybackOnNotesPlaybackFinished;
        _playback.Loop = true;
        _playback.RepeatStarted += PlaybackOnRepeatStarted;
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

        if (value != null)
        {

            if (value.DeviceType == OutputDeviceType.Asio)
            {
                try
                {
                    _outputDevice = new AsioSoundFontSynthDevice(value.DeviceName, "GeneralUser-GS.sf2");
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed to create audio device :(");
                    SelectedOutputDevice = null;
                }
            }
            else if (value.DeviceType == OutputDeviceType.Wasapi)
            {
                _outputDevice = new StandardSoundFontSynthDevice("GeneralUser-GS.sf2", value.Device as MMDevice);
            }
            else
            {
                _outputDevice = OutputDevice.GetByName(value.DeviceName);
            }


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
            if (note.Channel != SelectedChunk.ChannelId) continue;

            if (!Song.ChannelsById.TryGetValue(note.Channel, out var channel)) continue;

            var enu = Articulation.ArticulationToKitArticulation[Articulation.GmNoteToArticulation[note.NoteNumber]];

            if (!channel.LanesByNote.TryGetValue((int)enu, out var lanes)) continue;

            foreach (var lane in lanes)
            {
                if (lane.NotesByStartTimeTick.TryGetValue(note.Time, out var relatedNotes))
                {
                    foreach (var relatedNote in relatedNotes)
                    {
                        if (relatedNote.State == NoteState.Pending)
                        {
                            relatedNote.State = NoteState.Missed;
                        }

                        lane.StateChanged.Invoke(this, new StateChangeEventArgs(false));
                    }
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
        if (!IsPlaying) return;

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