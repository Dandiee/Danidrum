using Melanchall.DryWetMidi.Multimedia;
using NAudio.CoreAudioApi;
using NAudio.Wave.Asio;

namespace Danidrum.AudioEngine;

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
        
        return asioDevices.Concat(standardDevices).ToList();
    }
}