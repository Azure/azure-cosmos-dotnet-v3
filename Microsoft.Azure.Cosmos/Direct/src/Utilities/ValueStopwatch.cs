//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// ValueType equivalent of <see cref="Stopwatch"/>.
    /// 
    /// Used to avoid allocating when we need stopwatch-y functionality.
    /// 
    /// Deviates from <see cref="Stopwatch"/> in one semi-significant ways:
    ///   - rapidly calling Start() and then Stop() rapidly (allowing less than 1 tick to elapse) is going to drift
    ///     * this is weird thing to do, don't do it
    /// </summary>
    /// <seealso cref="Stopwatch"/>
    internal struct ValueStopwatch
    {
        private static readonly double ToTimeSpanTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        private static readonly double ToMilliseconds = 1_000 / (double)Stopwatch.Frequency;

        /// <seealso cref="Stopwatch.Frequency"/>
        public static readonly long Frequency = Stopwatch.Frequency;
        /// <seealso cref="Stopwatch.IsHighResolution"/>
        public static readonly bool IsHighResolution = Stopwatch.IsHighResolution;

        /// <remarks>
        /// We pack everything into a single long, so using this doesn't inflate any objects with it as a field.
        /// 
        /// State is interpreted as follows
        ///   - state == 0
        ///     * IsRunning == false
        ///     * We have never started
        ///   - state > 0
        ///     * IsRunning == true
        ///   - state < 0
        ///     * IsRunning == false
        ///     * We have been started and stopped
        ///     * ElapsedTicks == Math.Abs(state)
        ///
        /// We handle restarting the timer by backdating our start
        /// to account for any existing duration.
        /// </remarks>
        private long state;

        /// <seealso cref="Stopwatch.IsRunning"/>
        public readonly bool IsRunning
        => this.state > 0;

        /// <seealso cref="Stopwatch.ElapsedTicks"/>
        public readonly long ElapsedTicks
        {
            get
            {
                long stateCopy = this.state;

                if (stateCopy == 0)
                {
                    // haven't started
                    return 0;
                }

                if (stateCopy < 0)
                {
                    // we are stopped, the duration is stored in the 
                    // magnitude of state
                    return Math.Abs(stateCopy);
                }

                long effectiveStop = Stopwatch.GetTimestamp();

                long delta = effectiveStop - stateCopy;

                return delta;
            }
        }

        /// <seealso cref="Stopwatch.ElapsedMilliseconds"/>
        public readonly long ElapsedMilliseconds
        => (long)(this.ElapsedTicks * ValueStopwatch.ToMilliseconds);

        /// <seealso cref="Stopwatch.Elapsed"/>
        public readonly TimeSpan Elapsed
        {
            get
            {
                long stopwatchTicks = this.ElapsedTicks;
                long timeSpanTicks = (long)(stopwatchTicks * ValueStopwatch.ToTimeSpanTicks);

                return new TimeSpan(timeSpanTicks);
            }
        }

        /// <seealso cref="Stopwatch.Reset()"/>
        public void Reset()
        {
            this.state = 0;
        }

        /// <seealso cref="Stopwatch.Restart()"/>
        public void Restart()
        {
            this.Reset();
            this.Start();
        }

        /// <seealso cref="Stopwatch.Start()"/>
        public void Start()
        {
            long stateCopy = this.state;
            if (stateCopy > 0)
            {
                // already started
                return;
            }

            long now = Stopwatch.GetTimestamp();

            // if stateCopy == 0, we've never started so just store now
            // if stateCopy < 0 then Abs(stateCopy) == duration of last run
            //    so (now + stateCopy) backdates now so duration is preserved
            this.state = now + stateCopy;
        }

        /// <seealso cref="Stopwatch.Stop()"/>
        public void Stop()
        {
            long stateCopy = this.state;
            if (stateCopy <= 0)
            {
                // already stopped (or never started)
                return;
            }

            long effectiveStop = Stopwatch.GetTimestamp();

            long delta = effectiveStop - stateCopy;

            // when measuring small periods
            // delta can be < 0 due to bugs 
            // in BIOS or HAL
            //
            // force delta to be non-negative
            //
            // this mimics behavior in the BCL's
            // Stopwatch class
            delta = Math.Max(delta, 0);

            // if delta > 0 then some time has passed,
            // and we'll store -delta which Elapsed
            // will correctly deal with
            //
            // if delta == 0... then state == 0 which
            // will look like a new, unstarted, stopwatch
            // which ALSO has Elapsed == 0 and is fine
            this.state = -delta;
        }

        /// <seealso cref="Stopwatch.GetTimestamp()"/>
        public static long GetTimestamp()
        => Stopwatch.GetTimestamp();

        /// <seealso cref="Stopwatch.StartNew()"/>
        public static ValueStopwatch StartNew()
        {
            ValueStopwatch ret = default;
            ret.Start();

            return ret;
        }
    }
}