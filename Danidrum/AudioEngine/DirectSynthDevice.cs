using Danidrum.Context;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using MeltySynth;

namespace Danidrum.AudioEngine;

public class DirectSynthDevice(Synthesizer synth, ChunkContext Chunk) : IOutputDevice
{
    public void SendEvent(MidiEvent midiEvent)
    {

        switch (midiEvent)
        {
            case NoteOnEvent on: Process(on, 0x90, on.NoteNumber, on.Velocity); break;
            case NoteOffEvent off: Process(off, 0x80, off.NoteNumber, off.Velocity); break;
            case ProgramChangeEvent pc: Process(pc, 0xC0, Chunk.Instrument.Id, 0); break;
            case ControlChangeEvent cc: Process(cc, 0xB0, cc.ControlNumber, cc.ControlValue); break;
            case PitchBendEvent pb: Process(pb, 0xE0, pb.PitchValue & 0x7F, (pb.PitchValue >> 7) & 0x7F); break;
        }
    }

    private void Process(ChannelEvent midiEvent, int command, int data1, int data2)
        => synth.ProcessMidiMessage(midiEvent.Channel, command, data1, data2);

    public event EventHandler<MidiEventSentEventArgs>? EventSent;
    public void PrepareForEventsSending()
    {
        for (int i = 0; i < 16; i++)
        {
            synth.ProcessMidiMessage(i, 0xB0, 100, 0);   // RPN LSB
            synth.ProcessMidiMessage(i, 0xB0, 101, 0);   // RPN MSB
            synth.ProcessMidiMessage(i, 0xB0, 6, 24);    // Range = 12 semitones
            synth.ProcessMidiMessage(i, 0xB0, 100, 127); // Reset RPN
            synth.ProcessMidiMessage(i, 0xB0, 101, 127); // Reset RPN
        }
    }


    public void Dispose() { }
    public string Name => "DirectSynth";
}