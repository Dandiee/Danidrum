using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using MeltySynth;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.Wave.SampleProviders;
using System;

namespace Danidrum;

public class AsioSoundFontSynthDevice : IOutputDevice
{
    private readonly AsioOut _asioOut;
    private readonly MeltySynthSampleProvider _synthProvider; // Our new synth

    public AsioSoundFontSynthDevice(string asioDriverName, string soundFontPath)
    {
        // 1. Create the synth provider
        _synthProvider = new MeltySynthSampleProvider(soundFontPath, 44100);

        // 2. Initialize ASIO output and plug the synth into it
        _asioOut = new AsioOut(asioDriverName);
        _asioOut.Init(_synthProvider);
        _asioOut.Play();
    }

    public string Name => "AsioSoundFontSynthDevice";

    public void PrepareForEventsSending() { }

    /// <summary>
    /// This method just passes the MIDI event directly to the synth.
    /// No more voice management!
    /// </summary>
    public void SendEvent(MidiEvent midiEvent)
    {
        _synthProvider.ProcessMidiEvent(midiEvent);

        EventSent?.Invoke(this, new MidiEventSentEventArgs(midiEvent));
    }

    public event EventHandler<MidiEventSentEventArgs>? EventSent;

    public void Dispose()
    {
        _asioOut?.Stop();
        _asioOut?.Dispose();
    }
}

public class MeltySynthSampleProvider : ISampleProvider
{
    private readonly Synthesizer _synthesizer;
    public WaveFormat WaveFormat { get; }

    // Internal buffers for MeltySynth to render into (non-interleaved)
    private float[] _leftBuffer;
    private float[] _rightBuffer;

    public MeltySynthSampleProvider(string soundFontPath, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2); // Stereo
        _synthesizer = new Synthesizer(soundFontPath, sampleRate);

        // Initialize buffers to a reasonable default (e.g., 2048 samples)
        _leftBuffer = new float[2048];
        _rightBuffer = new float[2048];
    }

    /// <summary>
    /// This is where our custom IOutputDevice will send MIDI events.
    /// </summary>
    public void ProcessMidiEvent(MidiEvent midiEvent)
    {
        // Translate DryWetMidi event types to MeltySynth's ProcessMidiMessage.
        // The command byte (e.g., 0x90, 0xB0) is passed as an int.

        if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
        {
            // Command 0x90: Note On
            _synthesizer.ProcessMidiMessage(noteOn.Channel, 0x90, noteOn.NoteNumber, noteOn.Velocity);
        }
        else if (midiEvent is NoteOffEvent noteOff)
        {
            // Command 0x80: Note Off
            _synthesizer.ProcessMidiMessage(noteOff.Channel, 0x80, noteOff.NoteNumber, noteOff.Velocity);
        }
        else if (midiEvent is NoteOnEvent noteOnAsOff && noteOnAsOff.Velocity == 0)
        {
            // Handle NoteOn with Velocity 0 as a NoteOff (Standard MIDI practice)
            // Command 0x80: Note Off
            _synthesizer.ProcessMidiMessage(noteOnAsOff.Channel, 0x80, noteOnAsOff.NoteNumber, 0);
        }
        else if (midiEvent is ControlChangeEvent controlChange)
        {
            // Command 0xB0: Control Change
            _synthesizer.ProcessMidiMessage(controlChange.Channel, 0xB0, (int)controlChange.ControlNumber, controlChange.ControlValue);
        }
        else if (midiEvent is PitchBendEvent pitchBend)
        {
            // Command 0xE0: Pitch Bend
            int lsb = pitchBend.PitchValue & 0x7F;
            int msb = (pitchBend.PitchValue >> 7) & 0x7F;
            _synthesizer.ProcessMidiMessage(pitchBend.Channel, 0xE0, lsb, msb);
        }
        else if (midiEvent is ProgramChangeEvent programChange)
        {
            // Command 0xC0: Program Change
            _synthesizer.ProcessMidiMessage(programChange.Channel, 0xC0, programChange.ProgramNumber, 0);
        }
    }

    /// <summary>
    /// NAudio's AsioOut will call this method to get the audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // 'count' is the total interleaved samples requested (e.g., 1024)
        // 'samplesToRender' is the number of stereo *pairs* (e.g., 512)
        int samplesToRender = count / 2;

        // 1. Ensure our internal buffers are large enough
        if (_leftBuffer.Length < samplesToRender)
        {
            _leftBuffer = new float[samplesToRender];
            _rightBuffer = new float[samplesToRender];
        }

        // 2. Create spans from our internal buffers for MeltySynth
        var leftSpan = _leftBuffer.AsSpan(0, samplesToRender);
        var rightSpan = _rightBuffer.AsSpan(0, samplesToRender);

        // 3. Render audio into our *internal* L/R buffers
        _synthesizer.Render(leftSpan, rightSpan);

        // 4. Manually interleave the audio from our internal buffers
        //    into the 'buffer' (at 'offset') that NAudio provided.
        int outIndex = offset;
        for (int i = 0; i < samplesToRender; i++)
        {
            buffer[outIndex++] = _leftBuffer[i];
            buffer[outIndex++] = _rightBuffer[i];
        }

        return count;
    }
}