using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using MeltySynth;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.Wave.SampleProviders;
using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum;

public enum OutputDeviceType
{
    Wasapi,
    Asio,
    Midi
}

public record OutputAudioDevice(
    string DeviceName,
    string FriendlyName,
    OutputDeviceType DeviceType,
    bool IsDefault,
    object Device)
{
    public override string ToString() => FriendlyName;
}

public static class Audio
{
    public static IReadOnlyList<OutputAudioDevice> GetOutputDevices()
    {
        var asioDrivers = AsioDriver.GetAsioDriverNames();
        var asioDevices = asioDrivers.Select(driverName => new OutputAudioDevice(driverName, $"[ASIO] {driverName}", OutputDeviceType.Asio, false, null));
        
        using var enumerator = new MMDeviceEnumerator();
        var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var standardEndpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var standardDevices = standardEndpoints.Select(end => new OutputAudioDevice(end.ID, $"[WASAPI] {end.FriendlyName}", OutputDeviceType.Wasapi, defaultDevice.ID == end.ID, end));


        var midiDevices = OutputDevice.GetAll().Select(midi => new OutputAudioDevice(midi.Name, $"[MIDI] {midi.Name}", OutputDeviceType.Midi, false, null));
        
        return asioDevices.Concat(standardDevices).Concat(midiDevices).ToList();
    }
}

public class DistortionProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _drive;

    public DistortionProvider(ISampleProvider source, float drive)
    {
        _source = source;
        _drive = drive;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        // 1. Get clean audio from the Synth
        int samplesRead = _source.Read(buffer, offset, count);
        var outputGain = 0.1f;
        var drive = 50f;
        // 2. Distort it!
        for (int i = 0; i < samplesRead; i++)
        {
            float x = buffer[offset + i] * drive;
            // Soft Clipping: x / (1 + |x|)
            buffer[offset + i] = (x / (1.0f + Math.Abs(x)));
            buffer[offset + i] *= outputGain;
        }

        return samplesRead;
    }
}

public class DirectSynthDevice : IOutputDevice
{
    private readonly Synthesizer _synth;
    public DirectSynthDevice(Synthesizer synth) => _synth = synth;

    public void SendEvent(MidiEvent midiEvent)
    {
        // Direct mapping: No channel shifting needed because this Synth 
        // is dedicated ENTIRELY to this one instrument.
        if (midiEvent is NoteOnEvent on)
            _synth.ProcessMidiMessage(on.Channel, 0x90, on.NoteNumber, on.Velocity);
        else if (midiEvent is NoteOffEvent off)
            _synth.ProcessMidiMessage(off.Channel, 0x80, off.NoteNumber, off.Velocity);
        else if (midiEvent is ProgramChangeEvent pc)
            _synth.ProcessMidiMessage(pc.Channel, 0xC0, pc.ProgramNumber, 0);
        else if (midiEvent is ControlChangeEvent cc)
            _synth.ProcessMidiMessage(cc.Channel, 0xB0, (int)cc.ControlNumber, cc.ControlValue);
        else if (midiEvent is PitchBendEvent pb)
        {
            int lsb = pb.PitchValue & 0x7F;
            int msb = (pb.PitchValue >> 7) & 0x7F;
            _synth.ProcessMidiMessage(pb.Channel, 0xE0, lsb, msb);
        }
    }

    // Boilerplate for IOutputDevice interface
    public event EventHandler<MidiEventSentEventArgs>? EventSent;
    public void PrepareForEventsSending() { }
    public void Dispose() { }
    public string Name => "DirectSynth";
}

public class MultiTrackAudioEngine : IDisposable
{
    public MidiFile Midi { get; }
    private readonly SoundFont _sharedSoundFont;
    private readonly WasapiOut _asioOut;

    private readonly MixingSampleProvider _mixer;
    private readonly List<Playback> _activePlaybacks = new();

    public MultiTrackAudioEngine(MidiFile midi, string soundFontPath, MMDevice device)
    {
        Midi = midi;

        // 1. Load the heavy SoundFont data only ONCE to save RAM
        _sharedSoundFont = new SoundFont(soundFontPath);

        // 2. Prepare the master mixer (Standard stereo 44.1kHz)
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };

        // 3. Setup ASIO to play the Mixer's output
        _asioOut = new WasapiOut(device, AudioClientShareMode.Shared, true, 20);
        _asioOut.Init(_mixer);
        _asioOut.Play();
    }

    public void PlayMidiFile()
    {
        // Stop any existing playback
        Stop();

        var tempoMap = Midi.GetTempoMap();

        // 4. PROCESS EACH TRACK SEPARATELY
        foreach (var chunk in Midi.Chunks.OfType<TrackChunk>())
        {
            // A. Inspect the track to find what Instrument it is.
            // (You said you have ProgramChanges at time zero. Let's find the first one)
            var programChange = chunk.Events.OfType<ProgramChangeEvent>().FirstOrDefault();
            int instrumentId = programChange?.ProgramNumber ?? 0;

            // B. Create a DEDICATED Synth for this track
            var synth = new Synthesizer(_sharedSoundFont, 44100);

            // C. Setup Audio Provider
            // We use your 'MeltySynthSampleProvider' logic here (simplified)
            var audioProvider = new MeltySynthSampleProvider(synth);

            // D. APPLY EFFECTS based on Instrument ID
            // Check for Distortion Guitar (30), Overdrive (29), or your internal ID
            if (instrumentId == 30 || instrumentId == 29)
            {
                // Turn off reverb on the synth so it doesn't get muddy
                //synth.ReverbRoomSize = 0;

                // Wrap the provider in a Distortion Effect
                // (This adds the math we discussed earlier to the audio chain)
                var distortedProvider = new DistortionProvider(audioProvider, drive: 20.0f);
                _mixer.AddMixerInput(distortedProvider);
            }
            else
            {
                // Clean instrument - just add straight to mixer
                _mixer.AddMixerInput(audioProvider);
            }

            // E. Create a Playback for JUST this track
            // We route it to our "Virtual Device" which feeds the specific synth
            var trackPlayback = new Playback(chunk.GetTimedEvents(), tempoMap, new DirectSynthDevice(synth));
            _activePlaybacks.Add(trackPlayback);
        }

        // 5. Start all tracks simultaneously
        // Because they share the system clock, they will stay in sync
        foreach (var pb in _activePlaybacks)
        {
            pb.Start();
        }
    }

    public void Stop()
    {
        foreach (var pb in _activePlaybacks) pb.Dispose();
        _activePlaybacks.Clear();
        _mixer.RemoveAllMixerInputs();
    }

    public void Dispose()
    {
        Stop();
        _asioOut.Dispose();
    }
}


public class CustomEffectSynthProvider : ISampleProvider
{
    private readonly Synthesizer _cleanSynth;
    private readonly Synthesizer _dirtySynth;

    // Tracks which instrument is currently assigned to which MIDI channel (0-15)
    private readonly int[] _channelInstruments = new int[16];

    // Internal Audio Buffers
    private float[] _mixBufferL;
    private float[] _mixBufferR;
    private float[] _dirtyBufferL;
    private float[] _dirtyBufferR;

    public WaveFormat WaveFormat { get; }

    public CustomEffectSynthProvider()
    {
        
    }

    public CustomEffectSynthProvider(string soundFontPath, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2); // Stereo

        // 1. Initialize TWO synths with the same SoundFont
        _cleanSynth = new Synthesizer(soundFontPath, sampleRate);
        _dirtySynth = new Synthesizer(soundFontPath, sampleRate);

        // 2. Setup the "Dirty" synth for guitar duties
        // We disable reverb/chorus here so we can distort the raw signal without muddying the effects

        // 3. Initialize buffers (default size, will resize if needed)
        _mixBufferL = new float[2048];
        _mixBufferR = new float[2048];
        _dirtyBufferL = new float[2048];
        _dirtyBufferR = new float[2048];
    }

    // THIS is the method you call from your AsioSoundFontSynthDevice.SendEvent
    public void ProcessMidiEvent(MidiEvent midiEvent)
    {
        // A. Handle Program Changes (Instrument Swapping)
        // We need to track this so we know "Channel 3 is now a Guitar"
        if (midiEvent is ProgramChangeEvent pg)
        {
            _channelInstruments[pg.Channel] = pg.ProgramNumber;

            // We must update BOTH synths so they know which instrument is on which channel.
            // If we don't do this, the dirty synth might play a Piano sound when we route notes to it.
            SendToSynthRaw(_cleanSynth, pg);
            SendToSynthRaw(_dirtySynth, pg);
            return;
        }

        // B. Handle Note Events (The actual sound)
        if (midiEvent is NoteOnEvent || midiEvent is NoteOffEvent)
        {
            var channelEvent = (ChannelEvent)midiEvent;
            int currentInstrument = _channelInstruments[channelEvent.Channel];

            // LOGIC: If the instrument is Distortion Guitar (30) or Overdrive (29) or your target (31)
            // AND it is not the drum channel (10 is usually drums, represented as index 9 in 0-indexed)
            bool isGuitar = (currentInstrument == 30 || currentInstrument == 29 || currentInstrument == 31)
                            && channelEvent.Channel != 9;

            if (isGuitar)
            {

            }

            if (isGuitar)
            {
                // Send to the synth that will get the distortion effect
                SendToSynthRaw(_dirtySynth, midiEvent);
            }
            else
            {
                // Send to the clean synth
                SendToSynthRaw(_cleanSynth, midiEvent);
            }
        }
        // C. Handle Global Events (Pitch bend, Volume, Pan)
        else
        {
            // We must send these to BOTH synths. 
            // If you pan the track to the left, both the clean and dirty signal must move left.
            SendToSynthRaw(_cleanSynth, midiEvent);
            SendToSynthRaw(_dirtySynth, midiEvent);
        }
    }

    // The audio generation loop (Called by NAudio)
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesToRender = count / 2;
        EnsureBufferSizes(samplesToRender);

        // 1. Render the Clean Synth into the Mix Buffers
        _cleanSynth.Render(_mixBufferL.AsSpan(0, samplesToRender), _mixBufferR.AsSpan(0, samplesToRender));

        // 2. Render the Guitar Synth into the Dirty Buffers
        _dirtySynth.Render(_dirtyBufferL.AsSpan(0, samplesToRender), _dirtyBufferR.AsSpan(0, samplesToRender));

        // 3. APPLY DISTORTION (The Magic)
        ApplyDistortion(_dirtyBufferL, samplesToRender);
        ApplyDistortion(_dirtyBufferR, samplesToRender);

        // 4. Mix them together and write to output
        int outIndex = offset;
        for (int i = 0; i < samplesToRender; i++)
        {
            float left = _mixBufferL[i] + _dirtyBufferL[i];
            float right = _mixBufferR[i] + _dirtyBufferR[i];

            // Hard Limit to prevent clipping (crackling)
            if (left > 1.0f) left = 1.0f; else if (left < -1.0f) left = -1.0f;
            if (right > 1.0f) right = 1.0f; else if (right < -1.0f) right = -1.0f;

            buffer[outIndex++] = left;
            buffer[outIndex++] = right;
        }

        return count;
    }

    private void ApplyDistortion(float[] buffer, int count)
    {
        float drive = 20.0f; // High drive for heavy distortion
        float mixVolume = 0.7f; // Lower volume slightly so it doesn't overpower drums

        for (int i = 0; i < count; i++)
        {
            // Soft Clipping Algorithm
            float x = buffer[i] * drive;
            // x / (1 + |x|) rounds the waveform peaks
            buffer[i] = (x / (1.0f + Math.Abs(x))) * mixVolume;
        }
    }

    // Helper to translate DryWetMidi objects to MeltySynth commands
    private void SendToSynthRaw(Synthesizer synth, MidiEvent midiEvent)
    {
        if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
            synth.ProcessMidiMessage(noteOn.Channel, 0x90, noteOn.NoteNumber, noteOn.Velocity);

        else if (midiEvent is NoteOffEvent noteOff)
            synth.ProcessMidiMessage(noteOff.Channel, 0x80, noteOff.NoteNumber, noteOff.Velocity);

        else if (midiEvent is NoteOnEvent noteOnAsOff && noteOnAsOff.Velocity == 0)
            synth.ProcessMidiMessage(noteOnAsOff.Channel, 0x80, noteOnAsOff.NoteNumber, 0);

        else if (midiEvent is ControlChangeEvent cc)
            synth.ProcessMidiMessage(cc.Channel, 0xB0, (int)cc.ControlNumber, cc.ControlValue);

        else if (midiEvent is PitchBendEvent pb)
        {
            int lsb = pb.PitchValue & 0x7F;
            int msb = (pb.PitchValue >> 7) & 0x7F;
            synth.ProcessMidiMessage(pb.Channel, 0xE0, lsb, msb);
        }
        else if (midiEvent is ProgramChangeEvent pg)
            synth.ProcessMidiMessage(pg.Channel, 0xC0, pg.ProgramNumber, 0);
    }

    private void EnsureBufferSizes(int samples)
    {
        if (_mixBufferL.Length < samples)
        {
            _mixBufferL = new float[samples];
            _mixBufferR = new float[samples];
            _dirtyBufferL = new float[samples];
            _dirtyBufferR = new float[samples];
        }
    }
}

public class StandardSoundFontSynthDevice : IOutputDevice
{
    private readonly WasapiOut _output; // Using WasapiOut for standard audio
    private readonly CustomEffectSynthProvider _synthProvider;

    public StandardSoundFontSynthDevice(string soundFontPath, MMDevice device)
    {
        // 1. Define your sample rate

        int sampleRate = 44100;

        // 2. Create the synth provider
        _synthProvider = new CustomEffectSynthProvider(soundFontPath, sampleRate);

        // 3. Initialize WasapiOut (the non-ASIO driver)
        // We use "Shared" mode so it can play nicely with other Windows sounds.
        _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 20); // 20ms latency

        _output.Init(_synthProvider);
        _output.Play();
    }

    public string Name => "StandardSoundFontSynthDevice";

    public void PrepareForEventsSending() { }

    public void SendEvent(MidiEvent midiEvent)
    {
        // Pass the MIDI event directly to the synth
        _synthProvider.ProcessMidiEvent(midiEvent);

        EventSent?.Invoke(this, new MidiEventSentEventArgs(midiEvent));
    }

    public event EventHandler<MidiEventSentEventArgs>? EventSent;

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
    }
}

public class AsioSoundFontSynthDevice : IOutputDevice
{
    private readonly AsioOut _asioOut;
    private readonly CustomEffectSynthProvider _synthProvider; // Our new synth

    public AsioSoundFontSynthDevice(string asioDriverName, string soundFontPath)
    {
        //var sampleRate = driver.GetSampleRate();
        // 1. Create the synth provider
        _synthProvider = new CustomEffectSynthProvider(soundFontPath, 44100);

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

    public MeltySynthSampleProvider(Synthesizer synthesizer)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(synthesizer.SampleRate, 2); // Stereo
        _synthesizer = synthesizer;

        // Initialize buffers to a reasonable default (e.g., 2048 samples)
        _leftBuffer = new float[2048];
        _rightBuffer = new float[2048];
    }

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