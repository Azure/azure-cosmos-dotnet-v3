// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Holds the "just for testing"-bits of <see cref="ParallelPrefetch"/>.
    /// </summary>
    internal static partial class ParallelPrefetch
    {
        /// <summary>
        /// For testing purposes, provides ways to instrument <see cref="PrefetchInParallelAsync(IEnumerable{IPrefetcher}, int, ITrace, CancellationToken)"/>.
        /// 
        /// You shouldn't be using this outside of test projects.
        /// </summary>
        internal sealed class ParallelPrefetchTestConfig : ITrace
        {
            private ITrace innerTrace;

            private int startedTasks;
            private int awaitedTasks;

            public ArrayPool<IPrefetcher> PrefetcherPool { get; private set; }
            public ArrayPool<Task> TaskPool { get; private set; }
            public ArrayPool<object> ObjectPool { get; private set; }

            public int StartedTasks
            => this.startedTasks;

            public int AwaitedTasks
            => this.awaitedTasks;

            string ITrace.Name => this.innerTrace.Name;

            Guid ITrace.Id => this.innerTrace.Id;

            DateTime ITrace.StartTime => this.innerTrace.StartTime;

            TimeSpan ITrace.Duration => this.innerTrace.Duration;

            TraceLevel ITrace.Level => this.innerTrace.Level;

            TraceComponent ITrace.Component => this.innerTrace.Component;

            TraceSummary ITrace.Summary => this.innerTrace.Summary;

            ITrace ITrace.Parent => this.innerTrace.Parent;

            IReadOnlyList<ITrace> ITrace.Children => this.innerTrace.Children;

            IReadOnlyDictionary<string, object> ITrace.Data => this.innerTrace.Data;

            bool ITrace.IsBeingWalked => this.innerTrace.IsBeingWalked;

            public ParallelPrefetchTestConfig(
                ArrayPool<IPrefetcher> prefetcherPool,
                ArrayPool<Task> taskPool,
                ArrayPool<object> objectPool)
            {
                this.PrefetcherPool = prefetcherPool;
                this.TaskPool = taskPool;
                this.ObjectPool = objectPool;
            }

            public void SetInnerTrace(ITrace trace)
            {
                this.innerTrace = trace;
            }

            public void TaskStarted()
            {
                Interlocked.Increment(ref this.startedTasks);
            }

            public void TaskAwaited()
            {
                Interlocked.Increment(ref this.awaitedTasks);
            }

            ITrace ITrace.StartChild(string name)
            {
                return this.innerTrace.StartChild(name);
            }

            ITrace ITrace.StartChild(string name, TraceComponent component, TraceLevel level)
            {
                return this.innerTrace.StartChild(name, component, level);
            }

            void ITrace.AddDatum(string key, TraceDatum traceDatum)
            {
                this.innerTrace.AddDatum(key, traceDatum);
            }

            void ITrace.AddDatum(string key, object value)
            {
                this.innerTrace.AddDatum(key, value);
            }

            void ITrace.AddOrUpdateDatum(string key, object value)
            {
                this.innerTrace.AddOrUpdateDatum(key, value);
            }

            void ITrace.AddChild(ITrace trace)
            {
                this.innerTrace.AddChild(trace);
            }

            void IDisposable.Dispose()
            {
                this.innerTrace.Dispose();
            }
        }
    }
}
