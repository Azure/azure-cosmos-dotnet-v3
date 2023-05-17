//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;

    internal sealed class PerfCounters : IDisposable
    {
        public static PerfCounters Counters = new PerfCounters("DocDB Gateway", "Counters for DocDB Gateway");

        private string performanceCategory;

        private string performanceCategoryHelp;

        private PerformanceCounter frontendRequestsPerSec;

        private PerformanceCounter frontendActiveRequests;

        private PerformanceCounter admissionControlledRequestsPerSec;

        private PerformanceCounter admissionControlledRequests;

        private PerformanceCounter backendRequestsPerSec;

        private PerformanceCounter backendActiveRequests;

        private PerformanceCounter currentFrontendConnections;

        private PerformanceCounter fabricResolveServiceFailures;

        private PerformanceCounter queryRequestsPerSec;

        private PerformanceCounter triggerRequestsPerSec;

        private PerformanceCounter procedureRequestsPerSec;

        private PerformanceCounter averageProcedureRequestsDuration;

        private PerformanceCounter averageProcedureRequestsDurationBase;

        private PerformanceCounter averageQueryRequestsDuration;

        private PerformanceCounter averageQueryRequestsDurationBase;

        private PerformanceCounter backendConnectionOpenAverageLatency;

        private PerformanceCounter backendConnectionOpenAverageLatencyBase;

        private PerformanceCounter fabricResolveServiceAverageLatency;

        private PerformanceCounter fabricResolveServiceAverageLatencyBase;

        private PerformanceCounter routingFailures;

        private PerformanceCounter backendConnectionOpenFailuresDueToSynRetransmitPerSecond;

        private PerfCounters(string category, string categoryHelp)
        {
            performanceCategory = category;
            performanceCategoryHelp = categoryHelp;
        }

        public PerformanceCounter FrontendRequestsPerSec
        {
            get
            {
                return this.frontendRequestsPerSec;
            }
        }

        public PerformanceCounter FrontendActiveRequests
        {
            get
            {
                return this.frontendActiveRequests;
            }
        }
        public PerformanceCounter BackendRequestsPerSec
        {
            get
            {
                return this.backendRequestsPerSec;
            }
        }

        public PerformanceCounter BackendActiveRequests
        {
            get
            {
                return this.backendActiveRequests;
            }
        }

        public PerformanceCounter AdmissionControlledRequestsPerSec
        {
            get
            {
                return this.admissionControlledRequestsPerSec;
            }
        }

        public PerformanceCounter AdmissionControlledRequests
        {
            get
            {
                return this.admissionControlledRequests;
            }
        }

        public PerformanceCounter CurrentFrontendConnections
        {
            get
            {
                return this.currentFrontendConnections;
            }
        }

        public PerformanceCounter FabricResolveServiceFailures
        {
            get
            {
                return this.fabricResolveServiceFailures;
            }
        }

        public PerformanceCounter QueryRequestsPerSec
        {
            get
            {
                return this.queryRequestsPerSec;
            }
        }

        public PerformanceCounter TriggerRequestsPerSec
        {
            get
            {
                return this.triggerRequestsPerSec;
            }
        }

        public PerformanceCounter ProcedureRequestsPerSec
        {
            get
            {
                return this.procedureRequestsPerSec;
            }
        }

        public PerformanceCounter AverageProcedureRequestsDuration
        {
            get
            {
                return this.averageProcedureRequestsDuration;
            }
        }

        public PerformanceCounter AverageProcedureRequestsDurationBase
        {
            get
            {
                return this.averageProcedureRequestsDurationBase;
            }
        }

        public PerformanceCounter AverageQueryRequestsDuration
        {
            get
            {
                return this.averageQueryRequestsDuration;
            }
        }

        public PerformanceCounter AverageQueryRequestsDurationBase
        {
            get
            {
                return this.averageQueryRequestsDurationBase;
            }
        }

        public PerformanceCounter BackendConnectionOpenAverageLatency
        {
            get
            {
                return this.backendConnectionOpenAverageLatency;
            }
        }

        public PerformanceCounter BackendConnectionOpenAverageLatencyBase
        {
            get
            {
                return this.backendConnectionOpenAverageLatencyBase;
            }
        }

        public PerformanceCounter FabricResolveServiceAverageLatency
        {
            get
            {
                return this.fabricResolveServiceAverageLatency;
            }
        }

        public PerformanceCounter FabricResolveServiceAverageLatencyBase
        {
            get
            {
                return this.fabricResolveServiceAverageLatencyBase;
            }
        }

        public PerformanceCounter RoutingFailures
        {
            get
            {
                return this.routingFailures;
            }
        }

        public PerformanceCounter BackendConnectionOpenFailuresDueToSynRetransmitPerSecond
        {
            get
            {
                return this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond;
            }
        }

        /// <summary>
        /// Creating performance counter category is a privileged operation and
        /// hence done in the WinFab service setup entrypoint that is invoked before
        /// the service is actually started.
        /// </summary>
        public void InstallCounters()
        {
            CounterCreationDataCollection counters = new CounterCreationDataCollection();

            counters.Add(new CounterCreationData("Frontend Requests/sec", "Frontend Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Frontend Active Requests", "Frontend Active Requests", PerformanceCounterType.NumberOfItems32));

            counters.Add(new CounterCreationData("Admission Controlled Requests/sec", "Admission controlled requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Admission Controlled Requests", "Admission controlled requests", PerformanceCounterType.CounterDelta32));

            counters.Add(new CounterCreationData("Backend Requests/sec", "Backend Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Backend Active Requests", "Backend Active Requests", PerformanceCounterType.NumberOfItems32));

            counters.Add(new CounterCreationData("Current Frontend Connections", "Current Connections from Frontend to backend", PerformanceCounterType.NumberOfItems32));

            counters.Add(new CounterCreationData("Fabric Resolve Service Failures", "Number of failures for resolving a fabric service", PerformanceCounterType.CounterDelta32));

            counters.Add(new CounterCreationData("Query Requests/sec", "Query Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Trigger Requests/sec", "Trigger Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Procedure Requests/sec", "Procedure Requests per second", PerformanceCounterType.RateOfCountsPerSecond32));

            counters.Add(new CounterCreationData("Average Procedure Requests Duration", "Average Duration of a Procedure Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average Procedure Requests Duration Base", "Average Duration of a Procedure Request Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Average Query Requests Duration", "Average Duration of a Query Request", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Average Query Requests Duration Base", "Average Duration of a Query Request  Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Backend Connection Open Average Latency", "Average time to open a connection to the backend", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Backend Connection Open Average Latency Base", "Average time to open a connection to the backend Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Fabric Resolve Service Average Latency", "Average time to resolve a fabric service", PerformanceCounterType.AverageTimer32));

            counters.Add(new CounterCreationData("Fabric Resolve Service Average Latency Base", "Average time to resolve a fabric service  Base", PerformanceCounterType.AverageBase));

            counters.Add(new CounterCreationData("Routing Failures", "Number of failures for connecting to a stale replica", PerformanceCounterType.CounterDelta32));

            counters.Add(new CounterCreationData("Backend Connection Open Failures Due To Syn Retransmit Timeout/sec", "Number of failures per second when connecting to a backend node which failed with WSAETIMEDOUT", PerformanceCounterType.RateOfCountsPerSecond32));

            CreatePerfCounterCategory(performanceCategory, performanceCategoryHelp, PerformanceCounterCategoryType.SingleInstance, counters);
        }

        /// 
        /// Create a perf counter category with the provided name and creates the provided list of perf counters inside
        /// the category.
        ///
        public static void CreatePerfCounterCategory(string category, string categoryHelp,
            PerformanceCounterCategoryType categoryType,
            CounterCreationDataCollection counters)
        {
            // If the performance counter category already exists, check if any counters have changed.
            if (PerformanceCounterCategory.Exists(category))
            {
                PerformanceCounterCategory perfCategory = new PerformanceCounterCategory(category);
                bool shouldReturn = true;
                foreach (CounterCreationData counter in counters)
                {
                    try
                    {
                        if (!perfCategory.CounterExists(counter.CounterName))
                        {
                            shouldReturn = false;
                            break;
                        }
                    }
                    catch
                    {
                        shouldReturn = false;
                        break;
                    }
                }

                if (shouldReturn)
                {
                    return;
                }
                else
                {
                    PerformanceCounterCategory.Delete(category);
                }
            }

            // Create the category.
            PerformanceCounterCategory.Create(category, categoryHelp, categoryType, counters);
        }

        public void InitializePerfCounters()
        {
            this.frontendRequestsPerSec = new PerformanceCounter(performanceCategory, "Frontend Requests/sec", false);
            this.frontendRequestsPerSec.RawValue = 0;

            this.frontendActiveRequests = new PerformanceCounter(performanceCategory, "Frontend Active Requests", false);
            this.frontendActiveRequests.RawValue = 0;

            this.admissionControlledRequestsPerSec = new PerformanceCounter(performanceCategory, "Admission Controlled Requests/sec", false);
            this.admissionControlledRequestsPerSec.RawValue = 0;

            this.admissionControlledRequests = new PerformanceCounter(performanceCategory, "Admission Controlled Requests", false);
            this.admissionControlledRequests.RawValue = 0;

            this.backendRequestsPerSec = new PerformanceCounter(performanceCategory, "Backend Requests/sec", false);
            this.backendRequestsPerSec.RawValue = 0;

            this.backendActiveRequests = new PerformanceCounter(performanceCategory, "Backend Active Requests", false);
            this.backendActiveRequests.RawValue = 0;

            this.currentFrontendConnections = new PerformanceCounter(performanceCategory, "Current Frontend Connections", false);
            this.currentFrontendConnections.RawValue = 0;

            this.fabricResolveServiceFailures = new PerformanceCounter(performanceCategory, "Fabric Resolve Service Failures", false);
            this.fabricResolveServiceFailures.RawValue = 0;

            this.queryRequestsPerSec = new PerformanceCounter(performanceCategory, "Query Requests/sec", false);
            this.queryRequestsPerSec.RawValue = 0;

            this.triggerRequestsPerSec = new PerformanceCounter(performanceCategory, "Trigger Requests/sec", false);
            this.triggerRequestsPerSec.RawValue = 0;

            this.procedureRequestsPerSec = new PerformanceCounter(performanceCategory, "Procedure Requests/sec", false);
            this.procedureRequestsPerSec.RawValue = 0;

            this.averageProcedureRequestsDuration = new PerformanceCounter(performanceCategory, "Average Procedure Requests Duration", false);
            this.averageProcedureRequestsDuration.RawValue = 0;

            this.averageProcedureRequestsDurationBase = new PerformanceCounter(performanceCategory, "Average Procedure Requests Duration Base", false);
            this.averageProcedureRequestsDurationBase.RawValue = 0;

            this.averageQueryRequestsDuration = new PerformanceCounter(performanceCategory, "Average Query Requests Duration", false);
            this.averageQueryRequestsDuration.RawValue = 0;

            this.averageQueryRequestsDurationBase = new PerformanceCounter(performanceCategory, "Average Query Requests Duration Base", false);
            this.averageQueryRequestsDurationBase.RawValue = 0;

            this.backendConnectionOpenAverageLatency = new PerformanceCounter(performanceCategory, "Backend Connection Open Average Latency", false);
            this.backendConnectionOpenAverageLatency.RawValue = 0;

            this.backendConnectionOpenAverageLatencyBase = new PerformanceCounter(performanceCategory, "Backend Connection Open Average Latency Base", false);
            this.backendConnectionOpenAverageLatencyBase.RawValue = 0;

            this.fabricResolveServiceAverageLatency = new PerformanceCounter(performanceCategory, "Fabric Resolve Service Average Latency", false);
            this.fabricResolveServiceAverageLatency.RawValue = 0;

            this.fabricResolveServiceAverageLatencyBase = new PerformanceCounter(performanceCategory, "Fabric Resolve Service Average Latency Base", false);
            this.fabricResolveServiceAverageLatencyBase.RawValue = 0;

            this.routingFailures = new PerformanceCounter(performanceCategory, "Routing Failures", false);
            this.routingFailures.RawValue = 0;

            this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond = new PerformanceCounter(performanceCategory, "Backend Connection Open Failures Due To Syn Retransmit Timeout/sec", false);
            this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond.RawValue = 0;
        }

        #region IDisposable Members

        // Implement the dispose pattern.  The pattern is detailed at:
        //
        //  http://www.bluebytesoftware.com/blog/CategoryView,category,DesignGuideline.aspx
        //
        public void Dispose()
        {
            using (this.frontendActiveRequests) { }

            using (this.frontendRequestsPerSec) { }

            using (this.admissionControlledRequests) { }

            using (this.admissionControlledRequestsPerSec) { }

            using (this.backendActiveRequests) { }

            using (this.backendRequestsPerSec) { }

            using (this.currentFrontendConnections) { }

            using (this.fabricResolveServiceFailures) { }

            using (this.queryRequestsPerSec) { }

            using (this.triggerRequestsPerSec) { }

            using (this.procedureRequestsPerSec) { }

            using (this.averageProcedureRequestsDuration) { }

            using (this.averageProcedureRequestsDurationBase) { }

            using (this.averageQueryRequestsDuration) { }

            using (this.averageQueryRequestsDurationBase) { }

            using (this.backendConnectionOpenAverageLatency) { }

            using (this.backendConnectionOpenAverageLatencyBase) { }

            using (this.fabricResolveServiceAverageLatency) { }

            using (this.fabricResolveServiceAverageLatencyBase) { }

            using (this.routingFailures) { }

            using (this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond) { }
        }

        #endregion
    }
}
