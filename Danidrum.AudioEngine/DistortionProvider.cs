using NAudio.Wave;

namespace Danidrum.AudioEngine;

public class DistortionProvider(ISampleProvider source) : ISampleProvider
{
    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        // 1. Get clean audio from the Synth
        int samplesRead = source.Read(buffer, offset, count);
        var outputGain = 0.05f;
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