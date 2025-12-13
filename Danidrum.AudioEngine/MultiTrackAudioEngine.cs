using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using MeltySynth;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum.AudioEngine;

public record NotePlayedArgs(TrackChunk Track, NotesEventArgs NoteEventArgs);

public class MultiTrackAudioEngine : IDisposable
{
    public MidiFile Midi { get; }
    private readonly IWavePlayer _wavePlayer;

    public event EventHandler<NotePlayedArgs> OnNotePlayed;
    public event EventHandler OnRepeatStarted;

    private readonly List<Playback> _playbacks = [];
    private readonly Dictionary<Playback, TrackChunk> TrackMapping = new();

    private ITimeSpan _playbackStart;
    public ITimeSpan PlaybackStart
    {
        get => _playbackStart;
        set
        {
            _playbackStart = value;
            foreach (var playback in _playbacks)
            {
                playback.PlaybackStart = value;
            }
        }
    }


    private ITimeSpan _playbackEnd;
    public ITimeSpan PlaybackEnd
    {
        get => _playbackEnd;
        set
        {
            _playbackEnd = value;
            foreach (var playback in _playbacks)
            {
                playback.PlaybackEnd = value;
            }
        }
    }

    private bool _loop;
    public bool Loop
    {
        get => _loop;
        set
        {
            foreach (var playback in _playbacks)
            {
                playback.Loop = value;
            }
        }
    }

    public MultiTrackAudioEngine(MidiFile midi, string soundFontPath, OutputAudioDevice outputAudioDevice)
    {
        Midi = midi;
        
        var sharedSoundFont = new SoundFont(soundFontPath);
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };

        _wavePlayer = outputAudioDevice.DeviceType == OutputDeviceType.Asio
            ? new AsioOut(outputAudioDevice.DeviceName)
            : new WasapiOut(outputAudioDevice.Device as MMDevice, AudioClientShareMode.Shared, true, 20);

        var tempoMap = Midi.GetTempoMap();

        foreach (var chunk in Midi.Chunks.OfType<TrackChunk>())
        {
            var programChange = chunk.Events.OfType<ProgramChangeEvent>().FirstOrDefault();
            int instrumentId = programChange?.ProgramNumber ?? 0;
            var synth = new Synthesizer(sharedSoundFont, 44100);
            var audioProvider = new SampleProvider(synth);

            mixer.AddMixerInput(instrumentId == 30 || instrumentId == 29
                ? new DistortionProvider(audioProvider)
                : audioProvider);

            var trackPlayback = new Playback(chunk.GetTimedEvents(), tempoMap, new DirectSynthDevice(synth));

            TrackMapping[trackPlayback] = chunk;

            trackPlayback.NotesPlaybackFinished += TrackPlayback_NotesPlaybackFinished;

            _playbacks.Add(trackPlayback);
        }

        _playbacks[0].RepeatStarted += RepeatStarted;

        _wavePlayer.Init(mixer);
        _wavePlayer.Play();
    }

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