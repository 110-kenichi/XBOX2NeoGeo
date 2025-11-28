using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Zanac.XBOX2NeoGeo
{
    public sealed class HighResolutionTimer : IDisposable
    {
        private readonly Action _callback;
        private readonly long _intervalNs;
        private Thread _thread;
        private volatile bool _running;
        private readonly bool _useTimeBeginPeriod;

        public HighResolutionTimer(Action callback, long intervalNanoseconds, bool useTimeBeginPeriod = false)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (intervalNanoseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalNanoseconds));

            _callback = callback;
            _intervalNs = intervalNanoseconds;
            _useTimeBeginPeriod = useTimeBeginPeriod;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            if (_useTimeBeginPeriod)
                TimeBeginPeriod(1);

            _thread = new Thread(Run) { IsBackground = true, Name = "HighResolutionTimerThread" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            _thread?.Join();
            _thread = null;
            if (_useTimeBeginPeriod)
                TimeEndPeriod(1);
        }

        private void Run()
        {
            var sw = Stopwatch.StartNew();
            double ticksPerNanosecond = (double)Stopwatch.Frequency / 1_000_000_000.0;
            long intervalTicks = (long)(_intervalNs * ticksPerNanosecond);

            // spin threshold: how many ticks before target we switch to busy-wait
            long spinThresholdTicks = (long)(0.5e6 * ticksPerNanosecond); // default 0.5ms -> adjust if needed

            long next = sw.ElapsedTicks + intervalTicks;
            while (_running)
            {
                long now = sw.ElapsedTicks;
                long remaining = next - now;

                if (remaining > spinThresholdTicks)
                {
                    // coarse wait: compute milliseconds to sleep (leave 1ms margin)
                    long remainingNs = (long)(remaining / ticksPerNanosecond);
                    int sleepMs = (int)Math.Max(0, (remainingNs / 1_000_000) - 1);
                    if (sleepMs > 0)
                        Thread.Sleep(sleepMs);
                }
                else if (remaining > 0)
                {
                    // busy-wait until the target time
                    while (sw.ElapsedTicks < next) { Thread.SpinWait(10); }
                }

                // Trigger callback (may run slightly late/early)
                try { _callback(); } catch { /* swallow exceptions or handle as needed */ }

                // schedule next
                next += intervalTicks;

                // If we are far behind, skip missed intervals to avoid drift
                now = sw.ElapsedTicks;
                if (next <= now)
                {
                    // advance next until it's in the future
                    long missed = (now - next) / intervalTicks + 1;
                    next += missed * intervalTicks;
                }
            }
        }

        #region WinMM timeBeginPeriod (optional)
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        private static void TimeBeginPeriod(uint ms)
        {
            try { timeBeginPeriod(ms); } catch { /* ignored */ }
        }

        private static void TimeEndPeriod(uint ms)
        {
            try { timeEndPeriod(ms); } catch { /* ignored */ }
        }
        #endregion

        public void Dispose()
        {
            Stop();
        }
    }
}