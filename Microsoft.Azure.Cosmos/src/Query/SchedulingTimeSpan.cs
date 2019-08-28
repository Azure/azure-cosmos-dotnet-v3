//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// This struct is the TimeSpan equivalent to Stopwatch for SchedulingStopwatch.cs.
    /// That is to say that SchedulingStopwatch is behavior like a stopwatch (you can start and stop the stopwatch).
    /// SchedulingTimeSpan is a non mutable snapshot of SchedulingMetrics 
    /// </summary>
    internal struct SchedulingTimeSpan
    {
        /// <summary>
        /// The total time taken from when the process arrives to when it ended.
        /// </summary>
        private readonly TimeSpan turnaroundTime;

        /// <summary>
        /// The total latency (time) taken from when the process arrived to when the CPU actually started working on it.
        /// </summary>
        private readonly TimeSpan responseTime;

        /// <summary>
        /// The total time the process spent in the running state.
        /// </summary>
        private readonly TimeSpan runTime;

        /// <summary>
        /// The total time that the process spent is on the ready or waiting state.
        /// </summary>
        private readonly TimeSpan waitTime;

        /// <summary>
        /// Number of times the process was preempted.
        /// </summary>
        private readonly long numPreemptions;

        /// <summary>
        /// Initializes a new instance of the SchedulingTimeSpan struct.
        /// </summary>
        /// <param name="turnaroundTime">The total time taken from when the process arrives to when it ended.</param>
        /// <param name="responseTime">The total latency (time) taken from when the process arrived to when the CPU actually started working on it.</param>
        /// <param name="runTime">The total time the process spent in the running state.</param>
        /// <param name="waitTime">The total time the process spent in the waiting state.</param>
        /// <param name="numPreemptions">Number of times the process was preempted.</param>
        public SchedulingTimeSpan(TimeSpan turnaroundTime, TimeSpan responseTime, TimeSpan runTime, TimeSpan waitTime, long numPreemptions)
        {
            this.turnaroundTime = turnaroundTime;
            this.responseTime = responseTime;
            this.runTime = runTime;
            this.waitTime = waitTime;
            this.numPreemptions = numPreemptions;
        }

        /// <summary>
        /// Gets the number of preemptions (the number of times the process was moved from the running state to the ready state).
        /// </summary>
        public long NumPreemptions
        {
            get
            {
                return this.numPreemptions;
            }
        }

        /// <summary>
        /// Gets the total time from when the process arrived to when it ended.
        /// turnaround_time = end_time - arrival_time
        /// </summary>
        public TimeSpan TurnaroundTime
        {
            get
            {
                return this.turnaroundTime;
            }
        }

        /// <summary>
        /// Gets the total latency (time) from when the process arrived to when the CPU actually started working on it.
        /// response_time = start_time - arrival_time
        /// </summary>
        public TimeSpan ResponseTime
        {
            get
            {
                return this.responseTime;
            }
        }

        /// <summary>
        /// Gets the total time the process spent in the running state.
        /// </summary>
        public TimeSpan RunTime
        {
            get
            {
                return this.runTime;
            }
        }

        /// <summary>
        /// Gets the total time that a process was is in the ready or waiting state.
        /// wait_time = (end_time - arrival_time) - run_time = turnaround_time - run_time
        /// </summary>
        public TimeSpan WaitTime
        {
            get
            {
                return this.waitTime;
            }
        }

        #region StaticMethods
        /// <summary>
        /// Gets the average turnaround time for a list of SchedulingMetrics.
        /// </summary>
        /// <param name="schedulingTimeSpans">Metrics to get average turnaround times from.</param>
        /// <returns>The average turnaround time for a list of SchedulingMetrics.</returns>
        public static TimeSpan GetAverageTurnaroundTime(IEnumerable<SchedulingTimeSpan> schedulingTimeSpans)
        {
            return SchedulingTimeSpan.GetAverageTime(schedulingTimeSpans, schedulingMetric => schedulingMetric.TurnaroundTime.Ticks);
        }

        /// <summary>
        /// Gets the average response time for a list of SchedulingMetrics.
        /// </summary>
        /// <param name="schedulingTimeSpans">Metrics to get average response times from.</param>
        /// <returns>The average response time for a list of SchedulingMetrics.</returns>
        public static TimeSpan GetAverageResponseTime(IEnumerable<SchedulingTimeSpan> schedulingTimeSpans)
        {
            return SchedulingTimeSpan.GetAverageTime(schedulingTimeSpans, schedulingMetric => schedulingMetric.ResponseTime.Ticks);
        }

        /// <summary>
        /// Gets the average run time for a list of SchedulingMetrics.
        /// </summary>
        /// <param name="schedulingTimeSpans">Metrics to get average run times from.</param>
        /// <returns>The average run time for a list of SchedulingMetrics.</returns>
        public static TimeSpan GetAverageRunTime(IEnumerable<SchedulingTimeSpan> schedulingTimeSpans)
        {
            return SchedulingTimeSpan.GetAverageTime(schedulingTimeSpans, schedulingMetric => schedulingMetric.RunTime.Ticks);
        }

        /// <summary>
        /// Get the throughput which is the number of completed processes per time unit (second).
        /// </summary>
        /// <param name="schedulingTimeSpans">The scheduling metrics you wish to use.</param>
        /// <returns>The throughput for a list of scheduling Metrics</returns>
        public static double GetThroughput(IEnumerable<SchedulingTimeSpan> schedulingTimeSpans)
        {
            long totalTicksForAllProcesses = schedulingTimeSpans.Sum(schedulingMetric => schedulingMetric.TurnaroundTime.Ticks);
            TimeSpan totalTimeSpan = new TimeSpan(totalTicksForAllProcesses);
            double numberOfProcesses = schedulingTimeSpans.Count();
            return numberOfProcesses / totalTimeSpan.TotalSeconds;
        }

        /// <summary>
        /// Gets the CPU utilization (percent of time CPU is being used) for a list of SchedulingMetrics.
        /// </summary>
        /// <param name="schedulingTimeSpans">List of SchedulingMetrics to calculate the utilization from.</param>
        /// <returns>The CPU utilization for a list of SchedulingMetrics.</returns>
        public static double GetCpuUtilization(IEnumerable<SchedulingTimeSpan> schedulingTimeSpans)
        {
            long maxTurnaroundTime = schedulingTimeSpans.Max(schedulingMetric => schedulingMetric.TurnaroundTime.Ticks);
            long totalRunTimeTicks = schedulingTimeSpans.Sum(schedulingMetric => schedulingMetric.RunTime.Ticks);
            double totalRunTimeTicksDouble = Convert.ToInt64(totalRunTimeTicks);
            return totalRunTimeTicksDouble / maxTurnaroundTime;
        }
        #endregion

        /// <summary>
        /// Returns a string version of this SchedulingMetricsResult
        /// </summary>
        /// <returns>String version of the SchedulingMetricsResult.</returns>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Turnaround Time: {0}ms, Response Time: {1}ms, Run Time: {2}ms, Wait Time: {3}ms, Number Of Preemptions: {4}",
                this.TurnaroundTime.TotalMilliseconds,
                this.ResponseTime.TotalMilliseconds,
                this.RunTime.TotalMilliseconds,
                this.WaitTime.TotalMilliseconds,
                this.NumPreemptions);
        }

        /// <summary>
        /// Gets the average time for a list of scheduling metrics based on the property you wish to average over.
        /// </summary>
        /// <param name="schedulingTimeSpans">Metrics to get average times from.</param>
        /// <param name="propertySelectorCallback">Callback to use to select the desired property.</param>
        /// <returns>The average time for a list of scheduling metrics based on the property you wish to average over.</returns>
        private static TimeSpan GetAverageTime(IEnumerable<SchedulingTimeSpan> schedulingTimeSpans, Func<SchedulingTimeSpan, long> propertySelectorCallback)
        {
            if (schedulingTimeSpans == null)
            {
                throw new ArgumentNullException("schedulingTimeSpans");
            }

            if (schedulingTimeSpans.Count() == 0)
            {
                throw new ArgumentException("schedulingMetricsResults has no items.");
            }

            double doubleAverageTicks = schedulingTimeSpans.Average(propertySelectorCallback);
            long longAverageTicks = Convert.ToInt64(doubleAverageTicks);
            return new TimeSpan(longAverageTicks);
        }
    }
}
