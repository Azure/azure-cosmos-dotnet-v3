//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    internal sealed class TimerPool : IDisposable
    {
        private readonly Timer timer;
        private readonly ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> pooledTimersByTimeout;
        private readonly TimeSpan minSupportedTimeout;
        private readonly object timerConcurrencyLock;
        private readonly object subscriptionLock;
        private bool isRunning;
        private bool isDisposed;

        public TimerPool(int minSupportedTimerDelayInSeconds)
        {
            this.timerConcurrencyLock = new object();
            this.minSupportedTimeout = TimeSpan.FromSeconds(minSupportedTimerDelayInSeconds > 0 ? (double)minSupportedTimerDelayInSeconds : 1.0);
            this.pooledTimersByTimeout = new ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>>();
            this.timer = new Timer(
                new TimerCallback(this.OnTimer),
                state: (object)null,
                dueTime: TimeSpan.FromSeconds(1.0),
                period: TimeSpan.FromSeconds((double)minSupportedTimerDelayInSeconds));
            this.subscriptionLock = new object();
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
                throw new ObjectDisposedException(nameof(TimerPool));
            }
        }

        private void DisposeAllPooledTimers()
        {
            foreach (KeyValuePair<int, ConcurrentQueue<PooledTimer>> keyValuePair in this.pooledTimersByTimeout)
            {
                ConcurrentQueue<PooledTimer> concurrentQueue = keyValuePair.Value;
                PooledTimer result;
                while (concurrentQueue.TryDequeue(out result))
                {
                    result.CancelTimer();
                }
            }
            this.timer.Dispose();
        }

        private void OnTimer(object stateInfo)
        {
            lock (this.timerConcurrencyLock)
            {
                if (this.isRunning)
                {
                    return;
                }

                this.isRunning = true;
            }
            try
            {
                long ticks = DateTime.UtcNow.Ticks;
                foreach (KeyValuePair<int, ConcurrentQueue<PooledTimer>> keyValuePair in this.pooledTimersByTimeout)
                {
                    ConcurrentQueue<PooledTimer> concurrentQueue = keyValuePair.Value;
                    int count = keyValuePair.Value.Count;
                    long num = 0;
                    for (int index = 0; index < count; ++index)
                    {
                        PooledTimer result1;
                        if (concurrentQueue.TryPeek(out result1))
                        {
                            if (ticks < result1.TimeoutTicks)
                            {
                                break;
                            }

                            result1.FireTimeout();
                            num = result1.TimeoutTicks;
                            PooledTimer result2;
                            if (concurrentQueue.TryDequeue(out result2) && result2 != result1)
                            {
                                concurrentQueue.Enqueue(result2);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                lock (this.timerConcurrencyLock)
                {
                    this.isRunning = false;
                }
            }
        }

        public PooledTimer GetPooledTimer(int timeoutInSeconds)
        {
            this.ThrowIfDisposed();
            return new PooledTimer(
                timeoutInSeconds,
                this);
        }

        public long SubscribeForTimeouts(PooledTimer pooledTimer)
        {
            this.ThrowIfDisposed();
            TimeSpan timeSpan;
            if (pooledTimer.Timeout < this.minSupportedTimeout)
            {
                object[] objArray = new object[2];
                timeSpan = pooledTimer.Timeout;
                objArray[0] = (object)timeSpan.TotalSeconds;
                timeSpan = this.minSupportedTimeout;
                objArray[1] = (object)timeSpan.TotalSeconds;
                pooledTimer.Timeout = this.minSupportedTimeout;
            }
            lock (this.subscriptionLock)
            {
                ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> pooledTimersByTimeout1 = this.pooledTimersByTimeout;
                timeSpan = pooledTimer.Timeout;
                int totalSeconds1 = (int)timeSpan.TotalSeconds;
                ConcurrentQueue<PooledTimer> orAdd = new ConcurrentQueue<PooledTimer>();
                ref ConcurrentQueue<PooledTimer> local = ref orAdd;
                if (pooledTimersByTimeout1.TryGetValue(totalSeconds1, out local))
                {
                    orAdd.Enqueue(pooledTimer);
                }
                else
                {
                    ConcurrentDictionary<int, ConcurrentQueue<PooledTimer>> pooledTimersByTimeout2 = this.pooledTimersByTimeout;
                    timeSpan = pooledTimer.Timeout;
                    int totalSeconds2 = (int)timeSpan.TotalSeconds;
                    orAdd = pooledTimersByTimeout2.GetOrAdd(
                        totalSeconds2,
                        (Func<int, ConcurrentQueue<PooledTimer>>)(param1 => new ConcurrentQueue<PooledTimer>()));
                    orAdd.Enqueue(pooledTimer);
                }
                return DateTime.UtcNow.Ticks;
            }
        }
    }
}