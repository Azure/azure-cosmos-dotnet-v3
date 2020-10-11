// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Timers
{
    using System;
    using System.Threading.Tasks;

#nullable enable
    internal sealed class TimerWheelTimerCore : TimerWheelTimer
    {
        private static readonly object completedObject = new object();
        private readonly TaskCompletionSource<object> taskCompletionSource;
        private readonly object memberLock;
        private readonly TimerWheel timerWheel;
        private bool timerStarted = false;

        internal TimerWheelTimerCore(
            TimeSpan timeoutPeriod,
            TimerWheel timerWheel)
        {
            if (timeoutPeriod.Ticks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutPeriod));
            }

            this.timerWheel = timerWheel ?? throw new ArgumentNullException(nameof(timerWheel));
            this.Timeout = timeoutPeriod;
            this.taskCompletionSource = new TaskCompletionSource<object>();
            this.memberLock = new object();
        }

        public override TimeSpan Timeout { get; }

        public override Task StartTimerAsync()
        {
            lock (this.memberLock)
            {
                if (this.timerStarted)
                {
                    // use only once enforcement
                    throw new InvalidOperationException("Timer Already Started");
                }

                this.timerWheel.SubscribeForTimeouts(this);
                this.timerStarted = true;
                return this.taskCompletionSource.Task;
            }
        }

        public override bool CancelTimer()
        {
            return this.taskCompletionSource.TrySetCanceled();
        }

        public override bool FireTimeout()
        {
            return this.taskCompletionSource.TrySetResult(TimerWheelTimerCore.completedObject);
        }
    }
}