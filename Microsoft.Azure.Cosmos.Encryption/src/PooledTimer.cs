//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading.Tasks;

    internal sealed class PooledTimer
    {
        private readonly TaskCompletionSource<object> tcs;
        private readonly object memberLock;
        private long beginTicks;
        private TimeSpan timeoutPeriod;
        private TimerPool timerPool;
        private bool timerStarted;

        public PooledTimer(
            TimeSpan timeout,
            TimerPool timerPool)
        {
            this.timeoutPeriod = timeout;
            this.tcs = new TaskCompletionSource<object>();
            this.timerPool = timerPool ?? throw new ArgumentNullException(nameof(timerPool));
            this.memberLock = new object();
        }

        public long TimeoutTicks
        {
            get
            {
                return this.beginTicks + this.Timeout.Ticks;
            }
        }

        public TimeSpan Timeout
        {
            get
            {
                return this.timeoutPeriod;
            }
            set
            {
                this.timeoutPeriod = value;
            }
        }

        public Task StartTimerAsync()
        {
            lock (this.memberLock)
            {
                if (this.timerStarted)
                {
                    throw new InvalidOperationException("Timer Already Started");
                }

                this.beginTicks = this.timerPool.SubscribeForTimeouts(this);
                this.timerStarted = true;
                return this.tcs.Task;
            }
        }

        public bool CancelTimer()
        {
            return this.tcs.TrySetCanceled();
        }

        public bool FireTimeout()
        {
            return this.tcs.TrySetResult((object)null);
        }
    }
}