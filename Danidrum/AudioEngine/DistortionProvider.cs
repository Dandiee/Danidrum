using Danidrum.Context;
using NAudio.Wave;

namespace Danidrum.AudioEngine;

public class DistortionProvider(ISampleProvider source, ChunkContext chunk) : ISampleProvider
{
    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        // 1. Get clean audio from the Synth
        int samplesRead = source.Read(buffer, offset, count);
        var outputGain = chunk.Gain;
        var drive = chunk.Drive;

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