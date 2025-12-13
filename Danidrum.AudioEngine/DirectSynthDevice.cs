using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using MeltySynth;

namespace Danidrum.AudioEngine;

public class DirectSynthDevice(Synthesizer synth) : IOutputDevice
{
    public void SendEvent(MidiEvent midiEvent)
    {
        switch (midiEvent)
        {
            case NoteOnEvent on: Process(on, 0x90, on.NoteNumber, on.Velocity); break;
            case NoteOffEvent off: Process(off, 0x80, off.NoteNumber, off.Velocity); break;
            case ProgramChangeEvent pc: Process(pc, 0xC0, pc.ProgramNumber, 0); break;
            case ControlChangeEvent cc: Process(cc, 0xB0, cc.ControlNumber, cc.ControlValue); break;
            case PitchBendEvent pb: Process(pb, 0xE0, pb.PitchValue & 0x7F, (pb.PitchValue >> 7) & 0x7F); break;
        }
    }

    private void Process(ChannelEvent midiEvent, int command, int data1, int data2)
        => synth.ProcessMidiMessage(midiEvent.Channel, command, data1, data2);

    // Boilerplate for IOutputDevice interface
    public event EventHandler<MidiEventSentEventArgs>? EventSent;
    public void PrepareForEventsSending() { }
    public void Dispose() { }
    public string Name => "DirectSynth";
}