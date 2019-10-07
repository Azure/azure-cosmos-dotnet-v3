//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Diagnostics;

    /// <summary>
    /// This class keeps track of scheduling metrics for a single process using a stopwatch interface.
    /// Internally this class is composed of Stopwatches keeping track of scheduling metrics.
    /// The main metrics are turnaround, response, run, and wait time.
    /// However this class only handles behavior; if you want the data / results, then you will have to call on the 
    /// </summary>
    internal sealed class SchedulingStopwatch
    {
        /// <summary>
        /// Stopwatch used to measure turnaround time.
        /// </summary>
        private readonly Stopwatch turnaroundTimeStopwatch;

        /// <summary>
        /// Stopwatch used to measure response time.
        /// </summary>
        private readonly Stopwatch responseTimeStopwatch;

        /// <summary>
        /// Stopwatch used to measure runtime.
        /// </summary>
        private readonly Stopwatch runTimeStopwatch;

        /// <summary>
        /// Number of times the process was preempted.
        /// </summary>
        private long numPreemptions;

        /// <summary>
        /// Whether or not the process got a response yet.
        /// </summary>
        private bool responded;

        /// <summary>
        /// Initializes a new instance of the SchedulingStopwatch class.
        /// </summary>
        public SchedulingStopwatch()
        {
            this.turnaroundTimeStopwatch = new Stopwatch();
            this.responseTimeStopwatch = new Stopwatch();
            this.runTimeStopwatch = new Stopwatch();
        }

        /// <summary>
        /// Gets the SchedulingMetricsTimeSpan, which is a readonly snapshot of the SchedulingMetrics.
        /// </summary>
        /// <returns>the SchedulingMetricsResult.</returns>
        public SchedulingTimeSpan Elapsed
        {
            get
            {
                return new SchedulingTimeSpan(
                    this.turnaroundTimeStopwatch.Elapsed,
                    this.responseTimeStopwatch.Elapsed,
                    this.runTimeStopwatch.Elapsed,
                    this.turnaroundTimeStopwatch.Elapsed - this.runTimeStopwatch.Elapsed,
                    this.numPreemptions);
            }
        }

        /// <summary>
        /// Tells the SchedulingStopwatch know that the process is in a state where it is ready to be worked on,
        /// which in turn starts the stopwatch for for response time and turnaround time.
        /// </summary>
        public void Ready()
        {
            this.turnaroundTimeStopwatch.Start();
            this.responseTimeStopwatch.Start();
        }

        /// <summary>
        /// Starts or resumes the stopwatch for runtime meaning that the process in the run state for the first time
        /// or was preempted and now back in the run state.
        /// </summary>
        public void Start()
        {
            if (!this.runTimeStopwatch.IsRunning)
            {
                if (!this.responded)
                {
                    // This is the first time the process got a response, so the response time stopwatch needs to stop.
                    this.responseTimeStopwatch.Stop();
                    this.responded = true;
                }

                this.runTimeStopwatch.Start();
            }
        }

        public void Stop()
        {
            if (this.runTimeStopwatch.IsRunning)
            {
                this.runTimeStopwatch.Stop();
                this.numPreemptions++;
            }
        }

        /// <summary>
        /// Stops all the internal stopwatches.
        /// This is mainly useful for marking the end of a process to get an accurate turnaround time.
        /// It is undefined behavior to start a stopwatch that has been terminated.
        /// </summary>
        public void Terminate()
        {
            this.turnaroundTimeStopwatch.Stop();
            this.responseTimeStopwatch.Stop();
        }

        /// <summary>
        /// Returns a string version of this SchedulingStopwatch
        /// </summary>
        /// <returns>String version of the SchedulingStopwatch.</returns>
        public override string ToString()
        {
            // Just passing on to the SchedulingTimeSpan ToString function.
            return this.Elapsed.ToString();
        }
    }
}
