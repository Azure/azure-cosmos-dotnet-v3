// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal abstract class TimerWheel : IDisposable
    {
        public abstract void Dispose();

        public abstract TimerWheelTimer GetTimer(int timeoutInMs);

        public abstract void SubscribeForTimeouts(TimerWheelTimer timer);

        public static TimerWheel CreateTimerWheel(
            int resolutionInMs,
            int buckets)
        {
            return new Timers.TimerWheelCore(resolutionInMs, buckets);   
        }
    }
}