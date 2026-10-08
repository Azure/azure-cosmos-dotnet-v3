//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
#if NETSTANDARD20 || (NETFX && !NETFX45 && !NETFX461)
    using System.Threading.Channels;
#endif

    /// <summary>
    /// Producer/consumer queue used by
    /// <see cref="RntbdConnectionManager"/> to deliver versioned slot
    /// tokens from opener tasks to acquirers.
    /// </summary>
    /// <typeparam name="T">Token type (typically a slot identity struct).</typeparam>
    /// <remarks>
    /// Two implementations exist behind this interface, selected at
    /// compile time by the existing TFM-driven preprocessor symbols
    /// (<c>NETSTANDARD20</c>, <c>NETFX</c>, <c>NETFX45</c>, <c>NETFX461</c>):
    /// <list type="bullet">
    ///   <item><description>The primary implementation is a thin wrapper
    ///   over <c>System.Threading.Channels.Channel{T}</c>. Used on every
    ///   TFM where Channels 8.0.0 is available — net462+ (net48 / net481
    ///   in this project) and netstandard2.0. net461 is excluded because
    ///   Channels 8.0.0 requires net462+.</description></item>
    ///   <item><description>The fallback implementation is a hand-rolled
    ///   lock-based queue used on TFMs where Channels 8.0.0 isn't
    ///   supported (net45 / net461 / netstandard1.5 / netstandard1.6).
    ///   Maintained purely for backward compatibility with older SDK
    ///   targets.</description></item>
    /// </list>
    /// </remarks>
    internal interface ISlotQueue<T>
    {
        bool TryRead(out T item);
        bool TryPeek(out T item);
        Task<T> ReadAsync(CancellationToken ct);
        void Write(T item);
        void Complete();
    }

#if NETSTANDARD20 || (NETFX && !NETFX45 && !NETFX461)
    /// <summary>
    /// Primary <see cref="ISlotQueue{T}"/> implementation, backed by
    /// <see cref="System.Threading.Channels.Channel{T}"/>. Used on every
    /// TFM where Channels 8.0.0 is supported — net462+ (net48 / net481 in
    /// this project) and netstandard2.0+.
    /// </summary>
    internal sealed class SlotQueue<T> : ISlotQueue<T>
    {
        private readonly Channel<T> channel =
            System.Threading.Channels.Channel.CreateUnbounded<T>();

        public bool TryRead(out T item) => this.channel.Reader.TryRead(out item);

        public bool TryPeek(out T item) => this.channel.Reader.TryPeek(out item);

        public Task<T> ReadAsync(CancellationToken ct) =>
            this.channel.Reader.ReadAsync(ct).AsTask();

        public void Write(T item) => this.channel.Writer.TryWrite(item);

        public void Complete() => this.channel.Writer.TryComplete();
    }
#else
    /// <summary>
    /// Fallback <see cref="ISlotQueue{T}"/> implementation for TFMs where
    /// <c>System.Threading.Channels</c> 8.0.0 isn't supported (net45 /
    /// net461 / netstandard1.5 / netstandard1.6). Functionally equivalent
    /// to the Channel-based path but lock-based.
    /// </summary>
    /// <remarks>
    /// Notable correctness details:
    /// <list type="bullet">
    ///   <item><description>Waiter TCSes use
    ///   <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>
    ///   (referenced via integer cast 0x40 because the named enum value
    ///   isn't available on net45) so completion in <see cref="Write"/>
    ///   can't run continuations under the queue lock.</description></item>
    ///   <item><description>Cancellation registrations are disposed when
    ///   the wait completes so long-lived <see cref="CancellationToken"/>s
    ///   don't accumulate them.</description></item>
    ///   <item><description><see cref="Write"/> hands the item directly
    ///   to a waiter when possible. If the chosen waiter's TCS was
    ///   already completed (e.g., cancelled racing) the item is handed to
    ///   the next waiter (or enqueued) so it isn't silently lost.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class SlotQueue<T> : ISlotQueue<T>
    {
        private readonly Queue<T> items = new Queue<T>();
        private readonly Queue<TaskCompletionSource<T>> waiters = new Queue<TaskCompletionSource<T>>();
        private readonly object lockObj = new object();
        private bool completed;

        public bool TryRead(out T item)
        {
            lock (this.lockObj)
            {
                if (this.items.Count > 0)
                {
                    item = this.items.Dequeue();
                    return true;
                }
                item = default(T);
                return false;
            }
        }

        public bool TryPeek(out T item)
        {
            lock (this.lockObj)
            {
                if (this.items.Count > 0)
                {
                    item = this.items.Peek();
                    return true;
                }
                item = default(T);
                return false;
            }
        }

        public Task<T> ReadAsync(CancellationToken ct)
        {
            TaskCompletionSource<T> tcs;
            lock (this.lockObj)
            {
                if (this.items.Count > 0)
                {
                    return Task.FromResult(this.items.Dequeue());
                }

                if (this.completed)
                {
                    throw new InvalidOperationException("Queue is completed.");
                }

                tcs = new TaskCompletionSource<T>((TaskCreationOptions)0x40);
                this.waiters.Enqueue(tcs);
            }

            if (ct.CanBeCanceled)
            {
                CancellationTokenRegistration reg = ct.Register(
                    state => ((TaskCompletionSource<T>)state).TrySetCanceled(),
                    tcs);
                tcs.Task.ContinueWith(
                    (_, regObj) => ((CancellationTokenRegistration)regObj).Dispose(),
                    reg,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return tcs.Task;
        }

        public void Write(T item)
        {
            while (true)
            {
                TaskCompletionSource<T> waiter = null;
                lock (this.lockObj)
                {
                    // Silently drop writes once the queue is completed, to
                    // match Channel{T}.Writer.TryWrite returning false after
                    // TryComplete. Without this, items enqueued after
                    // Complete() would accumulate and never be drained.
                    if (this.completed)
                    {
                        return;
                    }
                    if (this.waiters.Count == 0)
                    {
                        this.items.Enqueue(item);
                        return;
                    }
                    waiter = this.waiters.Dequeue();
                }

                if (waiter.TrySetResult(item))
                {
                    return;
                }
                // Waiter already completed (cancelled). Try the next one.
            }
        }

        public void Complete()
        {
            List<TaskCompletionSource<T>> toFail;
            lock (this.lockObj)
            {
                if (this.completed)
                {
                    return;
                }
                this.completed = true;
                toFail = new List<TaskCompletionSource<T>>(this.waiters);
                this.waiters.Clear();
            }
            foreach (TaskCompletionSource<T> w in toFail)
            {
                w.TrySetException(new InvalidOperationException("Queue completed."));
            }
        }
    }
#endif
}
