// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal abstract class TimerWheel : IDisposable
    {
        public abstract void Dispose();

        /// <summary>
        /// Returns a <see cref="TimerWheelTimer"/> that can be used and started.
        /// </summary>
        /// <param name="timeoutInMs">A timeout in milliseconds</param>
        public abstract TimerWheelTimer GetTimer(int timeoutInMs);

        public abstract void SubscribeForTimeouts(TimerWheelTimer timer);

        /// <summary>
        /// Creates a new <see cref="TimerWheel"/> which is a simple timer wheel implementation
        /// </summary>
        /// <remarks>
        /// The <paramref name="resolutionInMs"/> defines the minimum supported timeout and <paramref name="buckets"/> times <paramref name="resolutionInMs"/> define the maximum supported timeout.
        /// </remarks>
        /// <param name="resolutionInMs">Amount of milliseconds per wheel slice.</param>
        /// <param name="buckets">Amount of slices in the wheel</param>
        public static TimerWheel CreateTimerWheel(
            int resolutionInMs,
            int buckets)
        {
            return new Timers.TimerWheelCore(resolutionInMs, buckets);   
        }
    }
}