using System;
using NAudio.Wave;

namespace DWMB_AIO.DWMB.Audio
{
    /// <summary>
    /// Plays a synthesized alarm tone (see <see cref="AlarmWaveProvider"/>) as a local,
    /// audible alert that a message arrived — in addition to, not instead of, the Discord
    /// notification. Discord can be muted, backgrounded, or simply not glanced at, so this
    /// gives a second, harder-to-miss channel.
    /// <para>
    /// NAudio's <see cref="WaveOutEvent"/> drives playback from its own callback thread, so
    /// <see cref="Trigger"/> is safe to call from the SharpPcap capture thread and
    /// <see cref="Silence"/> from the WPF UI thread without any dispatcher marshalling. All
    /// state is guarded by <see cref="gate"/> since both can be called concurrently.
    /// </para>
    /// </summary>
    sealed class AlarmPlayer : IDisposable
    {
        private readonly object gate = new();
        private WaveOutEvent? output;
        private bool disposed;

        /// <summary>Whether the alarm is currently sounding.</summary>
        public bool IsSounding { get; private set; }

        /// <summary>Raised whenever <see cref="IsSounding"/> changes. May fire off the UI thread.</summary>
        public event Action? StateChanged;

        /// <summary>
        /// Starts the alarm (quiet, ramping louder) if it isn't already sounding. A second
        /// call while it's already sounding is a no-op — a burst of messages doesn't restart
        /// the ramp or stack multiple alarms.
        /// </summary>
        public void Trigger()
        {
            lock (gate)
            {
                if (disposed || IsSounding)
                {
                    return;
                }

                // Play() just starts WaveOutEvent's own background playback thread and
                // returns immediately (unlike a network call), so it's safe to do this
                // under the lock — that also closes the window where Silence() could
                // race in and dispose newOutput between publishing it and starting it.
                var newOutput = new WaveOutEvent();
                newOutput.Init(new AlarmWaveProvider());
                newOutput.PlaybackStopped += OnPlaybackStopped;
                newOutput.Play();

                output = newOutput;
                IsSounding = true;
            }

            StateChanged?.Invoke();
        }

        /// <summary>Stops the alarm immediately, if it's sounding. Safe to call when it's not.</summary>
        public void Silence()
        {
            WaveOutEvent? toStop;
            lock (gate)
            {
                if (output == null)
                {
                    return;
                }

                toStop = output;
                output = null;
                IsSounding = false;
            }

            toStop.PlaybackStopped -= OnPlaybackStopped;
            toStop.Stop();
            toStop.Dispose();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Reconciles state if playback stops on its own (e.g. an audio device error)
        /// rather than via <see cref="Silence"/>, so the UI doesn't show a stuck "sounding"
        /// alarm with no way to clear it.
        /// </summary>
        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            lock (gate)
            {
                if (!ReferenceEquals(output, sender))
                {
                    // Already superseded/silenced elsewhere — that call owns cleanup.
                    return;
                }

                output = null;
                IsSounding = false;
            }

            (sender as WaveOutEvent)?.Dispose();
            StateChanged?.Invoke();
        }

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
            }

            Silence();
        }
    }
}
