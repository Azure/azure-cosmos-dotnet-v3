//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class PooledTimer
    {
        /// <summary>
        /// keeps track of the timer was started, timeout time is calculated using this
        /// </summary>
        private long beginTicks;

        /// <summary>
        /// TimeSpan to timeout
        /// </summary>
        private TimeSpan timeoutPeriod;

        /// <summary>
        /// PooledTimer subscribes to the TimerPool to get notified when the timeout has expired
        /// </summary>
#pragma warning disable IDE0044 // Add readonly modifier
        private TimerPool timerPool;
#pragma warning restore IDE0044 // Add readonly modifier

        /// <summary>
        /// tcs is set to completed state if the timeout occurs else its set to cancelled state 
        /// </summary>
        private readonly TaskCompletionSource<object> tcs;
        private int timerStarted = 0;

        public PooledTimer(int timeout, TimerPool timerPool)
            : this(TimeSpan.FromSeconds(timeout), timerPool)
        {
        }

        public PooledTimer(TimeSpan timeout, TimerPool timerPool)
        {
            this.timeoutPeriod = timeout;
            this.tcs = new TaskCompletionSource<object>();
            this.timerPool = timerPool;
        }

        /// <summary>
        /// this is the expected ticks when this timer should be fired
        /// </summary>
        public long TimeoutTicks
        {
            get { return this.beginTicks + this.Timeout.Ticks; }
        }

        /// <summary>
        /// amount of time in seconds after which the timeout should be fired
        /// </summary>
        public TimeSpan Timeout
        {
            get { return timeoutPeriod; }
            set { timeoutPeriod = value; }
        }

        /// <summary>
        /// Min supported Timeout for any timer.
        /// </summary>
        public TimeSpan MinSupportedTimeout
        {
            get { return this.timerPool.MinSupportedTimeout; }
        }

        /// <summary>
        /// Starts the timer for the timeout period specfied in constructor
        /// </summary>
        /// <returns>Returns the Task upon which you can await on until completion</returns>
        public Task StartTimerAsync()
        {
            int oldTimerStarted = Interlocked.Exchange(ref this.timerStarted, 1);

            if (oldTimerStarted != 0)
            {
                // use only once enforcement
                throw new InvalidOperationException("Timer Already Started");
            }

            this.beginTicks = this.timerPool.SubscribeForTimeouts(this);
            return this.tcs.Task;
        }

        /// <summary>
        /// Cancels the timer by setting the tcs state to cancelled
        /// </summary>
        public bool CancelTimer()
        {
            return this.tcs.TrySetCanceled();
        }

        /// <summary>
        /// Invoked by the TimerPool when the timeout period has elapsed
        /// If the state is already cancelled, its a noop else it changes the state to completed
        /// signalling a timeout on the TaskWaiter
        /// </summary>
        internal bool FireTimeout()
        {
            return this.tcs.TrySetResult(null);
        }
    }
}