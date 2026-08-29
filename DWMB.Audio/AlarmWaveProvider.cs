using System;
using NAudio.Wave;

namespace DWMB_AIO.DWMB.Audio
{
    /// <summary>
    /// Synthesizes a rising-and-falling siren tone entirely in code (no bundled audio
    /// asset needed) as 16-bit PCM mono audio. Volume starts quiet and ramps linearly up
    /// to <see cref="maxVolume"/> over <see cref="rampSeconds"/>, then holds there — so the
    /// alarm is noticeable but not jarring the instant a message arrives, and gets harder
    /// to ignore the longer it goes unacknowledged. Implements the plain <see
    /// cref="IWaveProvider"/> (rather than NAudio's float-based ISampleProvider) so
    /// playback only depends on the long-stable core wave-provider API.
    /// <para>
    /// Frequency is swept with a continuous phase accumulator (not re-derived from
    /// elapsed time each sample) so the tone has no clicks/discontinuities across buffer
    /// boundaries.
    /// </para>
    /// </summary>
    sealed class AlarmWaveProvider : IWaveProvider
    {
        private const double CenterFrequencyHz = 1100;
        private const double FrequencySpreadHz = 450;
        private const double SweepRateHz = 0.6; // one low->high->low cycle per ~1.7s, siren-like

        private readonly int sampleRate;
        private readonly double startVolume;
        private readonly double maxVolume;
        private readonly double rampSeconds;

        private double phase;
        private long sampleIndex;

        public AlarmWaveProvider(int sampleRate = 44100, double startVolume = 0.05, double maxVolume = 0.85, double rampSeconds = 20)
        {
            this.sampleRate = sampleRate;
            this.startVolume = startVolume;
            this.maxVolume = maxVolume;
            this.rampSeconds = rampSeconds;
            WaveFormat = new WaveFormat(sampleRate, 16, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesToWrite = count / 2; // 16-bit mono: 2 bytes per sample
            for (int i = 0; i < samplesToWrite; i++)
            {
                double t = sampleIndex / (double)sampleRate;
                double volume = rampSeconds <= 0
                    ? maxVolume
                    : Math.Min(maxVolume, startVolume + (maxVolume - startVolume) * (t / rampSeconds));

                double freq = CenterFrequencyHz + FrequencySpreadHz * Math.Sin(2 * Math.PI * SweepRateHz * t);
                phase += 2 * Math.PI * freq / sampleRate;
                if (phase > 2 * Math.PI)
                {
                    phase -= 2 * Math.PI;
                }

                short sample = (short)(Math.Sin(phase) * volume * short.MaxValue);
                int byteOffset = offset + i * 2;
                buffer[byteOffset] = (byte)(sample & 0xFF);
                buffer[byteOffset + 1] = (byte)((sample >> 8) & 0xFF);
                sampleIndex++;
            }

            return samplesToWrite * 2;
        }
    }
}
