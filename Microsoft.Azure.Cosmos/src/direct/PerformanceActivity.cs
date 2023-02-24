//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;

    internal class PerformanceActivity
    {
        private bool callCompleted;
        private static readonly long MaxTicks = TimeSpan.FromHours(6).Ticks;
        private Stopwatch stopWatch;

        private readonly PerformanceCounter requestsCounter;
        private readonly PerformanceCounter currentRequestsCounter;
        private readonly PerformanceCounter failuresCounter;

        private readonly PerformanceCounter averageTimeCounter;
        private readonly PerformanceCounter averageTimeBaseCounter;

        private string operationName;

        public delegate void OperationDurationDelegate(Guid ActivityId, string operation, double milliseconds);

#pragma warning disable 0649
        /// <summary>
        /// The delegate is populated by the assembly that desires to trace the <see cref="PerformanceActivity"/>.
        /// Hence ignore the warning "CS0649: field is never assigned to, and will always have its default value null"
        /// </summary>
        internal static OperationDurationDelegate OperationDuration;
#pragma warning restore 0649

        protected PerformanceActivity(
            PerformanceCounter requestsCounter,
            PerformanceCounter currentRequestsCounter,
            PerformanceCounter failuresCounter,
            PerformanceCounter averageTimeCounter,
            PerformanceCounter averageTimeBaseCounter,
            string operation
            )
        {
            this.callCompleted = false;
            this.requestsCounter = requestsCounter;
            this.currentRequestsCounter = currentRequestsCounter;
            this.failuresCounter = failuresCounter;

            this.averageTimeCounter = averageTimeCounter;
            this.averageTimeBaseCounter = averageTimeBaseCounter;
            this.operationName = operation;

            this.stopWatch = new Stopwatch();
        }


        protected string OperationName
        {
            get { return this.operationName; }
            set { this.operationName = value; }
        }

        public long ElapsedTicks
        {
            get
            {
                return stopWatch.ElapsedTicks;
            }
        }

        public double ElapsedMilliseconds
        {
            get
            {
                return stopWatch.Elapsed.TotalMilliseconds;
            }
        }

        protected Stopwatch StopWatch
        {
            get
            {
                return this.stopWatch;
            }
        }

        public void ActivityStart()
        {
            stopWatch.Start();

            if (this.requestsCounter != null)
            {
                this.requestsCounter.Increment();
            }

            if (this.currentRequestsCounter != null)
            {
                this.currentRequestsCounter.Increment();
            }
        }

        public virtual void ActivityComplete(bool success = true)
        {
            if (this.callCompleted == true)
            {
                return;
            }
            
            this.callCompleted = true;

            stopWatch.Stop();

            long ticks = stopWatch.ElapsedTicks;

            if (this.averageTimeCounter != null && ticks < PerformanceActivity.MaxTicks)
            {
                this.averageTimeCounter.IncrementBy(ticks);
                this.averageTimeBaseCounter.Increment();
            }

            if (this.operationName != null && PerformanceActivity.OperationDuration != null  && ticks < PerformanceActivity.MaxTicks)
            {
                PerformanceActivity.OperationDuration(Trace.CorrelationManager.ActivityId, this.operationName, stopWatch.ElapsedMilliseconds);
            }

            if (this.currentRequestsCounter != null)
            {
                this.currentRequestsCounter.Decrement();
            }

            if (success == false && this.failuresCounter != null)
            {
                this.failuresCounter.Increment();
            }
        }
    }

    internal sealed class RequestPerformanceActivity : PerformanceActivity
    {
        public RequestPerformanceActivity()
            : base(null, null, null, null, null, null)
        { }
    }
}
