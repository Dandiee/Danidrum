using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.Wave.SampleProviders;
using System;

namespace Danidrum;
public class AsioSineSynthOutputDevice : IOutputDevice
{
    private readonly AsioOut _asioOut;
    private readonly MixingSampleProvider _mixer;


    public AsioSineSynthOutputDevice(string asioDriverName)
    {
        // ASIO output
        _asioOut = new AsioOut(asioDriverName);


        // mixer (stereo, 44.1k)
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
        {
            ReadFully = true
        };


        _asioOut.Init(_mixer);
        _asioOut.Play();
    }


    public string Name => "AsioSineSynthOutputDevice";


    public void PrepareForEventsSending() { }


    public void SendEvent(MidiEvent midiEvent)
    {
        if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
        {
            var freq = MidiNoteToFrequency(noteOn.NoteNumber);
            var amp = noteOn.Velocity / 127f;
            var voice = new SineWaveProvider(freq, amp, durationSeconds: 1.5f);
            _mixer.AddMixerInput(voice);
        }
        else if (midiEvent is NoteOffEvent noteOff)
        {
            // For simplicity this demo does not implement voice tracking/stop.
        }
    }

    public event EventHandler<MidiEventSentEventArgs>? EventSent;


    public void Dispose()
    {
        _asioOut?.Stop();
        _asioOut?.Dispose();
    }


    private static double MidiNoteToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);
    }
}


// A trivial sine-wave provider for demo purposes
public class SineWaveProvider : ISampleProvider
{
    private readonly WaveFormat _waveFormat;
    private readonly float _amplitude;
    private readonly double _frequency;
    private readonly float _durationSeconds;
    private int _samplesGenerated;


    public SineWaveProvider(double frequency, float amplitude, float durationSeconds)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _frequency = frequency;
        _amplitude = amplitude;
        _durationSeconds = durationSeconds;
    }


    public WaveFormat WaveFormat => _waveFormat;


    public int Read(float[] buffer, int offset, int count)
    {
        int sampleRate = _waveFormat.SampleRate;
        int totalSamples = (int)(sampleRate * _durationSeconds);
        int samplesToWrite = Math.Min(count / 2, totalSamples - _samplesGenerated);


        for (int n = 0; n < samplesToWrite; n++)
        {
            double t = (double)(_samplesGenerated + n) / sampleRate;
            float sample = (float)(_amplitude * Math.Sin(2 * Math.PI * _frequency * t));


            buffer[offset + n * 2] = sample; // Left
            buffer[offset + n * 2 + 1] = sample; // Right
        }


        _samplesGenerated += samplesToWrite;
        return samplesToWrite * 2;
    }
}