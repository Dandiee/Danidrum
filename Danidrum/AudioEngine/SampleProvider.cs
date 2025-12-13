using Danidrum.Context;
using MeltySynth;
using NAudio.Wave;

namespace Danidrum.AudioEngine;

public class SampleProvider(Synthesizer synthesizer, ChunkContext chunk) : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(synthesizer.SampleRate, 2); // Stereo

    // Internal buffers for MeltySynth to render into (non-interleaved)
    private float[] _leftBuffer = new float[2048];
    private float[] _rightBuffer = new float[2048];

    // Initialize buffers to a reasonable default (e.g., 2048 samples)

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
        synthesizer.Render(leftSpan, rightSpan);

        // 4. Manually interleave the audio from our internal buffers
        //    into the 'buffer' (at 'offset') that NAudio provided.
        int outIndex = offset;
        for (int i = 0; i < samplesToRender; i++)
        {
            buffer[outIndex++] = _leftBuffer[i] * chunk.Volume;
            buffer[outIndex++] = _rightBuffer[i] * chunk.Volume;
        }

        return count;
    }
}