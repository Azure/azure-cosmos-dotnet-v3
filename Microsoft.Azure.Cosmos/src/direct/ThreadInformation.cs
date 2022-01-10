//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using Microsoft.Azure.Cosmos.Core.Trace;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ThreadInformation
    {
        private static readonly object lockObject = new object();

        internal int? AvailableThreads { get; }
        internal int? MinThreads { get; }
        internal int? MaxThreads { get; }
        internal bool? IsThreadStarving { get; }
        internal double? ThreadWaitIntervalInMs { get; }

        private static Stopwatch watch;
        private static Task task;

        public static ThreadInformation Get()
        {
            int? avlWorkerThreads = null;
            int? minWorkerThreads = null;
            int? maxWorkerThreads = null;

            ThreadInformation threadInfo = null;

            lock (lockObject)
            {

#if !(NETSTANDARD15 || NETSTANDARD16)
                ThreadPool.GetAvailableThreads(out int tempAvlWorkerThreads, out _);
                avlWorkerThreads = tempAvlWorkerThreads;
                ThreadPool.GetMinThreads(out int tempMinWorkerThreads, out _);
                minWorkerThreads = tempMinWorkerThreads;
                ThreadPool.GetMaxThreads(out int tempMaxWorkerThreads, out _);
                maxWorkerThreads = tempMaxWorkerThreads;
#endif

                bool? isThreadStarving = null;
                double? threadWaitIntervalInMs = null;

                //First time watch and task will be null
                if (ThreadInformation.watch != null && ThreadInformation.task != null)
                {
                    threadWaitIntervalInMs = ThreadInformation.watch.Elapsed.TotalMilliseconds;

                    // its thread starvation
                    // a) if total elapsed time for stopwatch was more than 1s
                    // b) last task failed due to some error
                    isThreadStarving = (threadWaitIntervalInMs > 1000 || ThreadInformation.task.IsFaulted);
                    
                    // If task is faulted, stop the watch manually. otherwise keep it running
                    if(ThreadInformation.task.IsFaulted && ThreadInformation.watch.IsRunning)
                    {
                        DefaultTrace.TraceError("Thread Starvation detection task failed. Exception: {0}", ThreadInformation.task.Exception);
                        ThreadInformation.watch.Stop();
                    }
                }

                // First time isThreadStarving, threadWaitIntervalInMs will be null.
                threadInfo = new ThreadInformation(
                   availableThreads: avlWorkerThreads,
                   minThreads: minWorkerThreads,
                   maxThreads: maxWorkerThreads,
                   isThreadStarving: isThreadStarving,
                   threadWaitIntervalInMs: threadWaitIntervalInMs);

                // if previous task is still not started running yet then do not reinitialize the watch (or new task).
                if (ThreadInformation.watch == null || !ThreadInformation.watch.IsRunning)
                {
                    // if last watch was stopped then reinitialize it
                    ThreadInformation.watch = Stopwatch.StartNew();

                    ThreadInformation.task = Task.Factory.StartNew(() =>
                    {
                        ThreadInformation.watch.Stop();
                    });
                }
            }

            return threadInfo;
        }

        private ThreadInformation(
            int? availableThreads, 
            int? minThreads, 
            int? maxThreads, 
            bool? isThreadStarving,
            double? threadWaitIntervalInMs)
        {
            this.AvailableThreads = availableThreads;
            this.MinThreads = minThreads;
            this.MaxThreads = maxThreads;
            this.IsThreadStarving = isThreadStarving;
            this.ThreadWaitIntervalInMs = threadWaitIntervalInMs;
        }

        public void AppendJsonString(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"isThreadStarving\":\"");
            if (this.IsThreadStarving.HasValue)
            {
                stringBuilder.Append(this.IsThreadStarving.Value).Append("\",");
            }
            else
            {
                stringBuilder.Append("no info\",");
            }

            if (this.ThreadWaitIntervalInMs.HasValue)
            {
                stringBuilder.Append("\"threadWaitIntervalInMs\":").Append(this.ThreadWaitIntervalInMs.Value).Append(",");
            }

            if (this.AvailableThreads.HasValue)
            {
                stringBuilder.Append("\"availableThreads\":").Append(this.AvailableThreads.Value).Append(",");
            }

            if (this.MinThreads.HasValue)
            {
                stringBuilder.Append("\"minThreads\":").Append(this.MinThreads.Value).Append(",");
            }

            if (this.MaxThreads.HasValue)
            {
                stringBuilder.Append("\"maxThreads\":").Append(this.MaxThreads.Value).Append(",");
            }

            stringBuilder.Length--; // Remove the extra comma at the end
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            if (this.IsThreadStarving.HasValue)
            {
                builder.Append("IsThreadStarving :").Append(this.IsThreadStarving.Value);
            } 

            if (this.ThreadWaitIntervalInMs.HasValue)
            {
                builder.Append(" ThreadWaitIntervalInMs :").Append(this.ThreadWaitIntervalInMs.Value);
            }

            if (this.AvailableThreads.HasValue)
            {
                builder.Append(" AvailableThreads :").Append(this.AvailableThreads.Value);
            }

            if (this.MinThreads.HasValue)
            {
                builder.Append(" MinThreads :").Append(this.MinThreads.Value);
            }

            if (this.MaxThreads.HasValue)
            {
                builder.Append(" MaxThreads :").Append(this.MaxThreads.Value);
            }

            return builder.ToString();
        }
    }
}