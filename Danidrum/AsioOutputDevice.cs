using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.Wave.SampleProviders;
using System;

namespace Danidrum;


public class AsioPolyphonicSynthDevice : IOutputDevice
{
    private readonly AsioOut _asioOut;
    private readonly MixingSampleProvider _mixer;
    private readonly List<SynthVoice> _voices;
    private readonly WaveFormat _waveFormat;

    // --- ADSR Parameters ---
    // You can expose these or hard-code them
    public float AttackSeconds { get; set; } = 0.01f;
    public float DecaySeconds { get; set; } = 0.1f;
    public float SustainLevel { get; set; } = 0.5f;
    public float ReleaseSeconds { get; set; } = 0.3f;
    // -----------------------

    public AsioPolyphonicSynthDevice(string asioDriverName)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _voices = new List<SynthVoice>();

        _mixer = new MixingSampleProvider(_waveFormat)
        {
            ReadFully = true
        };

        _asioOut = new AsioOut(asioDriverName);
        _asioOut.Init(_mixer);
        _asioOut.Play();
    }

    public string Name => "AsioPolyphonicSynthDevice";

    public void PrepareForEventsSending() { }

    public void SendEvent(MidiEvent midiEvent)
    {
        // --- Voice Management: Clean up dead voices ---
        // This is now done by checking the 'IsDead' property
        for (int i = _voices.Count - 1; i >= 0; i--)
        {
            if (_voices[i].IsDead)
            {
                _mixer.RemoveMixerInput(_voices[i]);
                _voices.RemoveAt(i);
            }
        }

        // --- Event Handling ---
        if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
            //_mixer.RemoveMixerInput(_voices[i]);
        {
            // Create a new voice with the specified ADSR parameters
            var voice = new SynthVoice(
                noteOn.NoteNumber,
                noteOn.Velocity,
                _waveFormat,
                AttackSeconds,
                DecaySeconds,
                SustainLevel,
                ReleaseSeconds);

            _voices.Add(voice);
            _mixer.AddMixerInput(voice);
        }
        else if (midiEvent is NoteOffEvent noteOff || (midiEvent is NoteOnEvent noteOffAsOn && noteOffAsOn.Velocity == 0))
        {
            int noteNumber = (midiEvent as NoteOffEvent)?.NoteNumber ?? (midiEvent as NoteOnEvent).NoteNumber;

            // Find active voices for this note and stop them
            foreach (var voice in _voices.Where(v => v.NoteNumber == noteNumber && !v.IsDead))
            {
                voice.Stop(); // Triggers the Release phase
            }
        }

        EventSent?.Invoke(this, new MidiEventSentEventArgs(midiEvent));
    }

    public event EventHandler<MidiEventSentEventArgs>? EventSent;

    public void Dispose()
    {
        _asioOut?.Stop();
        _asioOut?.Dispose();
        _voices.Clear();
    }
}

public class SynthVoice : ISampleProvider
{
    private readonly SignalGenerator _osc;
    private readonly EnvelopeGenerator _adsr;
    private readonly WaveFormat _monoWaveFormat;
    private float[] _monoBuffer; // Buffer to read mono samples from the oscillator

    public WaveFormat WaveFormat { get; } // This will be stereo
    public int NoteNumber { get; }

    /// <summary>
    // This is the correct way to check if the voice is finished.
    /// The EnvelopeState becomes Idle after the Release phase is complete.
    /// </summary>
    public bool IsDead => _adsr.State == EnvelopeGenerator.EnvelopeState.Idle;

    public SynthVoice(int noteNumber, int velocity, WaveFormat outputFormat,
                      float attackSeconds, float decaySeconds, float sustainLevel, float releaseSeconds)
    {
        NoteNumber = noteNumber;
        WaveFormat = outputFormat; // Expecting stereo

        // 1. Setup the ADSR Envelope Generator
        _adsr = new EnvelopeGenerator();
        _adsr.AttackRate = attackSeconds * WaveFormat.SampleRate;
        _adsr.DecayRate = decaySeconds * WaveFormat.SampleRate;
        _adsr.SustainLevel = sustainLevel;
        _adsr.ReleaseRate = releaseSeconds * WaveFormat.SampleRate;

        // 2. Setup the Oscillator (SignalGenerator)
        _monoWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(outputFormat.SampleRate, 1);
        double frequency = MidiNoteToFrequency(noteNumber);
        float amplitude = velocity / 127f;

        _osc = new SignalGenerator(_monoWaveFormat.SampleRate, _monoWaveFormat.Channels)
        {
            Type = SignalGeneratorType.SawTooth, // Sawtooth is richer
            Frequency = frequency,
            Gain = amplitude * 0.5 // Reduce gain to prevent clipping when mixing
        };

        // 3. Initialize mono buffer
        _monoBuffer = new float[outputFormat.SampleRate]; // Start with 1 sec buffer

        // Start the envelope (Attack phase)
        _adsr.Gate(true);
    }

    /// <summary>
    /// Triggers the Release phase of the envelope.
    /// </summary>
    public void Stop()
    {
        _adsr.Gate(false);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // If the envelope is idle, this voice is dead, return 0.
        if (IsDead)
        {
            return 0;
        }

        // 'count' is the total number of float samples requested (L/R)
        // 'samplesToGenerate' is the number of stereo pairs.
        int samplesToGenerate = count / WaveFormat.Channels;

        // Ensure our mono buffer is large enough
        if (_monoBuffer.Length < samplesToGenerate)
        {
            _monoBuffer = new float[samplesToGenerate];
        }

        // 1. Fill the mono buffer from the oscillator
        int monoSamplesRead = _osc.Read(_monoBuffer, 0, samplesToGenerate);

        // 2. Process the mono buffer: Apply envelope and write to stereo output
        for (int i = 0; i < monoSamplesRead; i++)
        {
            // Get the next envelope value
            float envelopeValue = _adsr.Process();

            // If envelope is dead, stop processing
            if (envelopeValue == 0)
            {
                // Mark as idle, so IsDead becomes true
                _adsr.Reset();
                break;
            }

            // Apply envelope to the mono sample
            float sample = _monoBuffer[i] * envelopeValue;

            // Write to stereo output buffer
            buffer[offset + i * 2] = sample;     // Left
            buffer[offset + i * 2 + 1] = sample; // Right
        }

        // Return total number of samples written (L/R)
        return monoSamplesRead * WaveFormat.Channels;
    }

    private static double MidiNoteToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);
    }
}