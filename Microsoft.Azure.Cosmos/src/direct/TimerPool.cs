//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// This class reduces the overhead associated with creating and disposing timers created for shortlived activities
    /// It creates a PooledTimer which when started, returns a Task that you can await on and which will complete if the timeout expires
    /// This is preferred over DelayTaskTimer since it only creates a single timer which is used for the lifetime of the pool.
    /// It can *only* fire the timers at the minimum granularity configured.
    /// </summary>
    internal sealed class TimerPool : IDisposable
    {
        private readonly Timer timer;
        private readonly ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> pooledTimersByTimeout;
        private readonly TimeSpan minSupportedTimeout;
        private readonly object timerConcurrencyLock; // protects isRunning to reject concurrent timer callback. Irrelevant to subscriptionLock.
        private bool isRunning = false;
        private bool isDisposed = false;

        public TimerPool(int minSupportedTimerDelayInSeconds)
        {
            this.timerConcurrencyLock = new Object();
            this.minSupportedTimeout = TimeSpan.FromSeconds(minSupportedTimerDelayInSeconds > 0 ? minSupportedTimerDelayInSeconds : 1);
            this.pooledTimersByTimeout = new ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>>();
            TimerCallback timerDelegate = new TimerCallback(OnTimer);
            this.timer = new Timer(timerDelegate, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(minSupportedTimerDelayInSeconds));
            DefaultTrace.TraceInformation("TimerPool Created with minSupportedTimerDelayInSeconds = {0}", minSupportedTimerDelayInSeconds);
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.DisposeAllPooledTimers();

            this.isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("TimerPool");
            }
        }

        private void DisposeAllPooledTimers()
        {
            DefaultTrace.TraceInformation("TimerPool Disposing");

            foreach (KeyValuePair<int, ConcurrentQueue<PooledTimer>> kv in this.pooledTimersByTimeout)
            {
                ConcurrentQueue<PooledTimer> pooledTimerQueue = kv.Value;
                PooledTimer timer;
                while (pooledTimerQueue.TryDequeue(out timer))
                {
                    timer.CancelTimer();
                }
            }

            this.timer.Dispose();
            DefaultTrace.TraceInformation("TimePool Disposed");
        }

        private void OnTimer(Object stateInfo)
        {
            lock(this.timerConcurrencyLock)
            {
                if(!this.isRunning)
                {
                    this.isRunning = true;
                }
                else
                {
                    return;
                }
            }
            try
            {
                // get the current tick count which will be used to compare to 
                // timeout duration and fire timeouts
                long currentTicks = DateTime.UtcNow.Ticks;

                foreach(KeyValuePair<int, ConcurrentQueue<PooledTimer>> kv in this.pooledTimersByTimeout)
                {
                    ConcurrentQueue<PooledTimer> pooledTimerQueue = kv.Value;
                    int count = kv.Value.Count;
                    long lastTicks = 0;

                    for(int nIndex = 0; nIndex < count; nIndex++)
                    {
                        PooledTimer pooledTimer;

                        // We keeping peeking, firing timeouts, and dequeuing until reach hit the first
                        // element whose timeout has not occcured.
                        if(pooledTimerQueue.TryPeek(out pooledTimer))
                        {
                            if(currentTicks >= pooledTimer.TimeoutTicks)
                            {
                                if(pooledTimer.TimeoutTicks < lastTicks)
                                {
                                    // Queue of timers should have expiry in increasing tick order
                                    DefaultTrace.TraceCritical("LastTicks: {0}, PooledTimer.Ticks: {1}",
                                        lastTicks,
                                        pooledTimer.TimeoutTicks);
                                }

                                pooledTimer.FireTimeout();
                                lastTicks = pooledTimer.TimeoutTicks;
                                PooledTimer timer;
                                if(pooledTimerQueue.TryDequeue(out timer))
                                {
                                    // this is purely a correctness check
                                    if (!ReferenceEquals(timer, pooledTimer))
                                    {
                                        // should never occur since there can only be 1 thread in this code at time.
                                        DefaultTrace.TraceCritical(
                                            "Timer objects peeked and dequeued are not equal");
                                        pooledTimerQueue.Enqueue(timer);
                                    }
                                }
                            }
                            else
                            {
                                // reached the element whose timeout has not yet expired,
                                // break out and move to the next queue.
                                break;
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                DefaultTrace.TraceCritical("Hit exception ex: {0}\n, stack: {1}", ex.Message, ex.StackTrace);
            }
            finally
            {
                lock(timerConcurrencyLock)
                {
                    isRunning = false;
                }
            }
        }

        internal ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> PooledTimersByTimeout
        {
            get
            {
                return this.pooledTimersByTimeout;
            }
        }

        /// <summary>
        /// get a timer with timeout specified in seconds
        /// </summary>
        /// <param name="timeoutInSeconds"></param>
        /// <returns></returns>
        public PooledTimer GetPooledTimer(int timeoutInSeconds)
        {
            this.ThrowIfDisposed();
            return new PooledTimer(timeoutInSeconds, this);
        }

        /// <summary>
        /// Start the countdown for timeout
        /// </summary>
        /// <param name="pooledTimer"></param>
        /// <returns>the begin ticks of the timer</returns>
        public long SubscribeForTimeouts(PooledTimer pooledTimer)
        {
            this.ThrowIfDisposed();
            if(pooledTimer.Timeout < this.minSupportedTimeout)
            {
                DefaultTrace.TraceWarning("Timer timeoutinSeconds {0} is less than minSupportedTimeoutInSeconds {1}, will use the minsupported value",
                    pooledTimer.Timeout.TotalSeconds,
                    this.minSupportedTimeout.TotalSeconds);
                pooledTimer.Timeout = this.minSupportedTimeout;
            }

            if (!this.pooledTimersByTimeout.TryGetValue((int)pooledTimer.Timeout.TotalSeconds, out ConcurrentQueue<PooledTimer> timerQueue))
            {
                timerQueue = this.pooledTimersByTimeout.GetOrAdd((int)pooledTimer.Timeout.TotalSeconds,
                    (_) => new ConcurrentQueue<PooledTimer>());
            }

            // in order to enqueue timers into a queue by their TimeoutTicks, do TimeoutTicks generation and enqueue atomically.
            lock (timerQueue)
            {
                timerQueue.Enqueue(pooledTimer);
                return DateTime.UtcNow.Ticks;
            }
        }
    }
}
