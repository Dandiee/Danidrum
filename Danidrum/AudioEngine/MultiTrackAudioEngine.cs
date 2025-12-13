using Danidrum.Context;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using MeltySynth;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum.AudioEngine;

public record NotePlayedArgs(ChunkContext Chunk, NotesEventArgs NoteEventArgs);

public class MultiTrackAudioEngine : IDisposable
{
    public SongContext Song { get; }
    private readonly IWavePlayer _wavePlayer;

    public event EventHandler<NotePlayedArgs> OnNotePlayed;
    public event EventHandler? OnRepeatStarted;

    private readonly List<Playback> _playbacks = [];
    private readonly Dictionary<Playback, ChunkContext> TrackMapping = new();

    public ITimeSpan PlaybackStart
    {
        get => _playbacks[0].PlaybackStart;
        set
        {
            foreach (var playback in _playbacks)
            {
                playback.PlaybackStart = value;
            }
        }
    }
    public ITimeSpan PlaybackEnd
    {
        get => _playbacks[0].PlaybackEnd;
        set
        {
            foreach (var playback in _playbacks)
            {
                playback.PlaybackEnd = value;
            }
        }
    }
    public bool Loop
    {
        get => _playbacks[0].Loop;
        set
        {
            foreach (var playback in _playbacks)
            {
                playback.Loop = value;
            }
        }
    }
    public double Speed
    {
        get => _playbacks[0].Speed;
        set
        {
            foreach (var playback in _playbacks)
            {
                playback.Speed = value;
            }
        }
    }

    public MultiTrackAudioEngine(SongContext song, string soundFontPath, OutputAudioDevice outputAudioDevice)
    {
        Song = song;
        
        var sharedSoundFont = new SoundFont(soundFontPath);
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };

        _wavePlayer = outputAudioDevice.DeviceType == OutputDeviceType.Asio
            ? new AsioOut(outputAudioDevice.DeviceName)
            : new WasapiOut(outputAudioDevice.Device as MMDevice, AudioClientShareMode.Shared, true, 20);

         
        var tempoMap = Song.Midi.GetTempoMap();

        foreach (var chunk in Song.Chunks)
        {
            var synth = new Synthesizer(sharedSoundFont, 44100);
            var audioProvider = new SampleProvider(synth);

            mixer.AddMixerInput(chunk.UseDistortion
                ? new DistortionProvider(audioProvider, chunk)
                : audioProvider);
             
            var trackPlayback = new Playback(chunk.TrackChunk.GetTimedEvents(), tempoMap, new DirectSynthDevice(synth, chunk));

            TrackMapping[trackPlayback] = chunk;

            trackPlayback.NotesPlaybackFinished += TrackPlayback_NotesPlaybackFinished;
            trackPlayback.NoteCallback = (data, time, length, playbackTime) => NoteCallback(chunk, data, time, length, playbackTime);

            _playbacks.Add(trackPlayback);
        }

        _playbacks[0].RepeatStarted += RepeatStarted;

        _wavePlayer.Init(mixer);
        _wavePlayer.Play();
    }

    private NotePlaybackData NoteCallback(ChunkContext chunk, NotePlaybackData rawNoteData, long rawTime,
        long rawLength, TimeSpan playbackTime)
        => chunk.IsMuted ? null : rawNoteData;

    public void MoveToTime(ITimeSpan timeSpan)
    {
        foreach (var playback in _playbacks)
        {
            playback.MoveToTime(timeSpan);
        }
    }

    public double GetCurrentTime() => _playbacks[0].GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;

    private void RepeatStarted(object? sender, EventArgs e) => OnRepeatStarted?.Invoke(this, e);

    private void TrackPlayback_NotesPlaybackFinished(object? sender, NotesEventArgs e)
    {
        var track = TrackMapping[(sender as Playback)!];
        OnNotePlayed?.Invoke(this, new NotePlayedArgs(track, e));
    }

    public void Play()
    {
        Stop();

        foreach (var playback in _playbacks)
        {
            playback.Start();
        }
    }

    public void Stop()
    {
        foreach (var playback in _playbacks)
        {
            playback.Stop();
        }
    }

    public void Dispose()
    {
        Stop();
        _wavePlayer.Dispose();
    }
}