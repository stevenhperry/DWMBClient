using System;
using NAudio.Wave;

namespace DWMB_AIO.DWMB.Audio
{
    /// <summary>
    /// Synthesizes the alarm tone entirely in code (no bundled audio asset needed) as
    /// 16-bit PCM mono audio, in three phases measured from when the alarm started:
    /// <list type="bullet">
    /// <item>0–<see cref="rampSeconds"/>: a sweeping siren, volume ramping linearly from
    /// <see cref="startVolume"/> up to <see cref="maxVolume"/> — noticeable but not
    /// jarring the instant a message arrives.</item>
    /// <item><see cref="rampSeconds"/>–<see cref="criticalAfterSeconds"/>: the same siren,
    /// held at <see cref="maxVolume"/>.</item>
    /// <item>past <see cref="criticalAfterSeconds"/>: a distinct, harsher tone — a fixed
    /// high pitch gated into rapid beeps rather than a smooth sweep — at
    /// <see cref="maxVolume"/> continuously, so an alarm that's gone unacknowledged for a
    /// full minute sounds unmistakably more urgent than one that just started.</item>
    /// </list>
    /// Implements the plain <see cref="IWaveProvider"/> (rather than NAudio's float-based
    /// ISampleProvider) so playback only depends on the long-stable core wave-provider API.
    /// <para>
    /// The siren's frequency is swept with a continuous phase accumulator (not re-derived
    /// from elapsed time each sample) so it has no clicks/discontinuities across buffer
    /// boundaries; the critical tone's beeps are similarly ramped in/out over a few
    /// milliseconds (<see cref="BeepEdgeSeconds"/>) rather than gated with a hard on/off
    /// cut, which would otherwise pop audibly since the cut isn't aligned to a zero
    /// crossing.
    /// </para>
    /// </summary>
    sealed class AlarmWaveProvider : IWaveProvider
    {
        // Phase 1-2: sweeping siren (quiet-to-loud, then held at full volume).
        private const double SirenCenterFrequencyHz = 1100;
        private const double SirenFrequencySpreadHz = 450;
        private const double SirenSweepRateHz = 0.6; // one low->high->low cycle per ~1.7s

        // Phase 3: fixed-pitch tone gated into rapid beeps — deliberately harsher/more
        // insistent than the siren above, to signal an alarm that's gone unacknowledged.
        private const double CriticalToneHz = 1800;
        private const double CriticalBeepRateHz = 4; // beeps per second
        private const double CriticalBeepDutyCycle = 0.5; // fraction of each beep period that's "on"
        private const double BeepEdgeSeconds = 0.01; // fade in/out per beep, to avoid clicking

        private readonly int sampleRate;
        private readonly double startVolume;
        private readonly double maxVolume;
        private readonly double rampSeconds;
        private readonly double criticalAfterSeconds;

        private double sirenPhase;
        private double criticalPhase;
        private long sampleIndex;

        public AlarmWaveProvider(
            int sampleRate = 44100,
            double startVolume = 0.05,
            double maxVolume = 1.0,
            double rampSeconds = 30,
            double criticalAfterSeconds = 60)
        {
            this.sampleRate = sampleRate;
            this.startVolume = startVolume;
            this.maxVolume = maxVolume;
            this.rampSeconds = rampSeconds;
            this.criticalAfterSeconds = criticalAfterSeconds;
            WaveFormat = new WaveFormat(sampleRate, 16, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesToWrite = count / 2; // 16-bit mono: 2 bytes per sample
            for (int i = 0; i < samplesToWrite; i++)
            {
                double t = sampleIndex / (double)sampleRate;
                double sampleValue = t < criticalAfterSeconds
                    ? NextSirenSample(t)
                    : NextCriticalSample(t - criticalAfterSeconds);

                short sample = (short)(sampleValue * short.MaxValue);
                int byteOffset = offset + i * 2;
                buffer[byteOffset] = (byte)(sample & 0xFF);
                buffer[byteOffset + 1] = (byte)((sample >> 8) & 0xFF);
                sampleIndex++;
            }

            return samplesToWrite * 2;
        }

        private double NextSirenSample(double t)
        {
            double volume = rampSeconds <= 0
                ? maxVolume
                : Math.Min(maxVolume, startVolume + (maxVolume - startVolume) * (t / rampSeconds));

            double freq = SirenCenterFrequencyHz + SirenFrequencySpreadHz * Math.Sin(2 * Math.PI * SirenSweepRateHz * t);
            sirenPhase += 2 * Math.PI * freq / sampleRate;
            if (sirenPhase > 2 * Math.PI)
            {
                sirenPhase -= 2 * Math.PI;
            }

            return Math.Sin(sirenPhase) * volume;
        }

        private double NextCriticalSample(double criticalT)
        {
            criticalPhase += 2 * Math.PI * CriticalToneHz / sampleRate;
            if (criticalPhase > 2 * Math.PI)
            {
                criticalPhase -= 2 * Math.PI;
            }

            double beepPeriod = 1.0 / CriticalBeepRateHz;
            double onDuration = beepPeriod * CriticalBeepDutyCycle;
            double edge = Math.Min(BeepEdgeSeconds, onDuration / 2);
            double positionInBeep = criticalT % beepPeriod;

            double envelope;
            if (positionInBeep >= onDuration)
            {
                envelope = 0; // between beeps
            }
            else if (positionInBeep < edge)
            {
                envelope = positionInBeep / edge; // fade in
            }
            else if (positionInBeep > onDuration - edge)
            {
                envelope = (onDuration - positionInBeep) / edge; // fade out
            }
            else
            {
                envelope = 1;
            }

            return Math.Sin(criticalPhase) * maxVolume * envelope;
        }
    }
}
