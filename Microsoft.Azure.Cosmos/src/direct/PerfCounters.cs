//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.ServiceFramework.Core;

    /// <summary>
    /// Front end performance counters, and the helper that registers a counter category with Windows.
    /// </summary>
    /// <remarks>
    /// A counter category is registered once per machine by a privileged process, and is then opened by
    /// the service process. Registering writes to HKLM and needs administrator rights, while the service
    /// itself runs unelevated, so the two halves live in different executables and can never share an
    /// object. The only thing they have in common is the category name, which is why the names on this
    /// class are constants that both halves reference. When the two disagree the service starts, tries to
    /// open counters in a category nothing registered, and cannot recover, because registration only
    /// happens in the setup step.
    ///
    ///   PROCESS A: ...Host.ServiceFabric.EntryPoint.exe  |  PROCESS B: ...Host.ServiceFabric.exe
    ///   RunAsPolicy EntryPointType=Setup, LocalSystem    |  the service account, unelevated
    ///   runs on every activation, then exits             |  runs for the life of the service
    ///                                                    |
    ///     new PerfCounters(category, help)               |    PerfCounters.Initialize(category, help)
    ///          |                                         |         |
    ///          +--- InstallCounters()                    |         +--- new PerfCounters(category, help)
    ///                  |                                 |         +--- InitializePerfCounters()
    ///                  |  registers the category and     |                  |  opens one
    ///                  |  the names of its counters      |                  |  PerformanceCounter per
    ///                  v                                 |                  v  counter, RawValue = 0
    ///        +====================================+      |         Counters = instance
    ///        |  Windows category registration     |      |         (published only once fully built)
    ///        |  survives process exit and reboot  |      |                  |
    ///        +====================================+      |                  v
    ///                  ^                                 |         RequestDispatcher and
    ///                  +---------------------------------+         HttpContextWrapper read the
    ///                    process B opens what A registered          static and increment
    ///
    /// The instance built in process A is thrown away on purpose. InstallCounters assigns no field; it
    /// only needs the category name and help text. The instance built in process B is kept, because it
    /// holds the PerformanceCounter handles that the shared HTTP stack reports through.
    ///
    /// Re-running the setup step is safe. CreatePerfCounterCategory returns without touching anything
    /// when the category already exists with the same type and the same counters, so counter values
    /// survive the frequent activations Service Fabric performs. It deletes and recreates the category
    /// when the shape changed, which resets the values and leaves stale handles in any process that has
    /// already opened them. That is why counters that fall out of use are labelled here rather than
    /// removed.
    ///
    /// Counters is process wide and must never be null, because every emission site dereferences it
    /// without a guard. It starts on the Gateway category so a process that never claims one still
    /// works, and Initialize refuses a second, different category rather than let two services report
    /// into the same counters.
    /// </remarks>
    internal sealed class PerfCounters : IDisposable
    {
        private static readonly object InitializeLock = new object();

        private static string claimedCategory;

        private readonly string performanceCategory;

        private readonly string performanceCategoryHelp;

        // Written by RequestDispatcher: front end request flow and admission control.
        private PerformanceCounter frontendRequestsPerSec;

        private PerformanceCounter frontendActiveRequests;

        private PerformanceCounter admissionControlledRequestsPerSec;

        private PerformanceCounter admissionControlledRequests;

        // Written by TransportClient and HttpTransportClient: back end request flow.
        private PerformanceCounter backendRequestsPerSec;

        private PerformanceCounter backendActiveRequests;

        private PerformanceCounter routingFailures;

        // Written by PerformanceActivities: query and stored procedure timings, and back end connect latency.
        private PerformanceCounter queryRequestsPerSec;

        private PerformanceCounter procedureRequestsPerSec;

        private PerformanceCounter averageQueryRequestsDuration;

        private PerformanceCounter averageQueryRequestsDurationBase;

        private PerformanceCounter averageProcedureRequestsDuration;

        private PerformanceCounter averageProcedureRequestsDurationBase;

        private PerformanceCounter backendConnectionOpenAverageLatency;

        private PerformanceCounter backendConnectionOpenAverageLatencyBase;

        // Written by Connection.
        private PerformanceCounter backendConnectionOpenFailuresDueToSynRetransmitPerSecond;

        // Not written by any caller today. They are installed and bound, so they exist in the
        // category and read as zero. Left in place because removing a counter changes the
        // category shape, which forces a delete and recreate on the next privileged setup.
        private PerformanceCounter currentFrontendConnections;

        private PerformanceCounter triggerRequestsPerSec;

        /// <summary>Category owned by the RoutingGateway. ManagementFrontend, ResourceProvider and CosmosFabric report into it as well.</summary>
        internal const string GatewayCategory = "DocDB Gateway";

        /// <summary>Help text for <see cref="GatewayCategory"/>.</summary>
        internal const string GatewayCategoryHelp = "Counters for DocDB Gateway";

        /// <summary>Category owned by the ControllerService.</summary>
        internal const string ControllerServiceCategory = "Cosmos ControllerService";

        /// <summary>Help text for <see cref="ControllerServiceCategory"/>.</summary>
        internal const string ControllerServiceCategoryHelp = "Counters for Cosmos ControllerService";

        /// <summary>Category owned by the CentralAllocationService.</summary>
        internal const string CentralAllocationServiceCategory = "Cosmos CentralAllocationService";

        /// <summary>Help text for <see cref="CentralAllocationServiceCategory"/>.</summary>
        internal const string CentralAllocationServiceCategoryHelp = "Counters for Cosmos CentralAllocationService";

        /// <summary>
        /// Creates a counter set bound to <paramref name="category"/>.
        /// </summary>
        /// <remarks>
        /// A service must build the setup entry point and the service process instances from the same
        /// category constant, otherwise the category installed is not the category written to.
        /// </remarks>
        internal PerfCounters(string category, string categoryHelp)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentNullException(nameof(category));
            }

            this.performanceCategory = category;
            this.performanceCategoryHelp = categoryHelp;
        }

        /// <summary>
        /// The counter set the shared HTTP stack reports through. A service claims it by calling
        /// <see cref="Initialize"/> during startup; anything that does not claim it keeps reporting
        /// into the historical Gateway category.
        /// </summary>
        internal static PerfCounters Counters { get; private set; } = new PerfCounters(GatewayCategory, GatewayCategoryHelp);

        /// <summary>
        /// Binds this process to <paramref name="category"/> and creates its counters.
        /// </summary>
        /// <remarks>
        /// Call once during startup, before the request dispatcher is started, using the same category
        /// constant the setup entry point installed from. The counters are created before the instance
        /// is published so a request thread can never observe a half built counter set.
        ///
        /// Calling this again with the same category is allowed, because Service Fabric can close and
        /// re-open a service instance inside one process. Calling it with a different category throws:
        /// the counter set is process wide, so it cannot serve two services at once.
        /// </remarks>
        internal static void Initialize(string category, string categoryHelp)
        {
            lock (PerfCounters.InitializeLock)
            {
                if (PerfCounters.claimedCategory != null
                    && !string.Equals(PerfCounters.claimedCategory, category, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Performance counters are already bound to '{PerfCounters.claimedCategory}' in this process and cannot also serve '{category}'.");
                }

                PerfCounters instance = new PerfCounters(category, categoryHelp);
                instance.InitializePerfCounters();

                PerfCounters.Counters = instance;
                PerfCounters.claimedCategory = category;
            }
        }

        /// <summary>
        /// Releases the process wide claim so tests do not leak counter state into each other.
        /// </summary>
        internal static void ResetForTesting()
        {
            lock (PerfCounters.InitializeLock)
            {
                PerfCounters.Counters = new PerfCounters(GatewayCategory, GatewayCategoryHelp);
                PerfCounters.claimedCategory = null;
            }
        }

        // Written by RequestDispatcher: front end request flow and admission control.
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

        // Written by TransportClient and HttpTransportClient: back end request flow.
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

        public PerformanceCounter RoutingFailures
        {
            get
            {
                return this.routingFailures;
            }
        }

        // Written by PerformanceActivities: query and stored procedure timings, and back end connect latency.
        public PerformanceCounter QueryRequestsPerSec
        {
            get
            {
                return this.queryRequestsPerSec;
            }
        }

        public PerformanceCounter ProcedureRequestsPerSec
        {
            get
            {
                return this.procedureRequestsPerSec;
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

        // Written by Connection.
        public PerformanceCounter BackendConnectionOpenFailuresDueToSynRetransmitPerSecond
        {
            get
            {
                return this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond;
            }
        }

        // Not written by any caller today. They are installed and bound, so they exist in the
        // category and read as zero. Left in place because removing a counter changes the
        // category shape, which forces a delete and recreate on the next privileged setup.
        public PerformanceCounter CurrentFrontendConnections
        {
            get
            {
                return this.currentFrontendConnections;
            }
        }

        public PerformanceCounter TriggerRequestsPerSec
        {
            get
            {
                return this.triggerRequestsPerSec;
            }
        }

        /// <summary>
        /// Creates the given performance counter category.
        /// </summary>
        /// <param name="category">Name of the category.</param>
        /// <param name="categoryHelp">Help description.</param>
        /// <param name="categoryType">Category type. Also part of the equivalence check against an existing category.</param>
        /// <param name="counters">Counters in the category.</param>
        /// <param name="useSystemMutex">
        /// Indicates whether machine-wide synchronization should be used to avoid races between different entry-points attempting to create the same category.
        /// </param>
        /// <remarks>
        /// If the category already exists it is checked against <paramref name="categoryType"/> and
        /// <paramref name="counters"/>, and is deleted and recreated when the type differs or a counter is
        /// missing. The type has to take part in that check because a SingleInstance category rejects
        /// counters opened with an instance name (and a MultiInstance category requires one), so a caller
        /// that changes the type would otherwise silently keep the old, incompatible category and only fail
        /// later when the counters are initialized. A category whose type cannot be read is treated as a
        /// mismatch and recreated.
        /// </remarks>
        internal static void CreatePerfCounterCategory(string category,
            string categoryHelp,
            PerformanceCounterCategoryType categoryType,
            CounterCreationDataCollection counters,
            bool useSystemMutex = true)
        {
            SystemSynchronizationScope syncScope = useSystemMutex ? SystemSynchronizationScope.CreateSynchronizationScope($"CDBPerfCategory-{category}") : default;

            try
            {
                // If the performance counter category already exists, check whether its type or any
                // of its counters have changed.
                if (PerformanceCounterCategory.Exists(category))
                {
                    PerformanceCounterCategory perfCategory = new PerformanceCounterCategory(category);
                    bool shouldReturn = true;

                    // The category type has to match as well as the counter names. A category
                    // installed as SingleInstance rejects counters opened with an instance name
                    // ("Category '<name>' is marked as single-instance.") and vice versa, so a
                    // scope that changes the type must recreate the category rather than reuse it.
                    try
                    {
                        if (perfCategory.CategoryType != categoryType)
                        {
                            shouldReturn = false;
                        }
                    }
                    catch
                    {
                        shouldReturn = false;
                    }

                    if (shouldReturn)
                    {
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
            finally
            {
                syncScope?.Dispose();
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

            // The three "Fabric Resolve Service" counters below are created in the category but have
            // no field, property or writer, so nothing can ever record into them. Left in place
            // because removing a counter changes the category shape, which forces a delete and
            // recreate on the next privileged setup.
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

            PerfCounters.CreatePerfCounterCategory(
                this.performanceCategory,
                this.performanceCategoryHelp,
                PerformanceCounterCategoryType.SingleInstance,
                counters);
        }

        /// <summary>
        /// Binds the counters to this instance's category.
        /// </summary>
        /// <remarks>
        /// The category must already have been created by <see cref="InstallCounters"/> from the setup
        /// entry point, using the same category name.
        /// </remarks>
        public void InitializePerfCounters()
        {
            // Written by RequestDispatcher: front end request flow and admission control.
            this.frontendRequestsPerSec = new PerformanceCounter(this.performanceCategory, "Frontend Requests/sec", false);
            this.frontendRequestsPerSec.RawValue = 0;

            this.frontendActiveRequests = new PerformanceCounter(this.performanceCategory, "Frontend Active Requests", false);
            this.frontendActiveRequests.RawValue = 0;

            this.admissionControlledRequestsPerSec = new PerformanceCounter(this.performanceCategory, "Admission Controlled Requests/sec", false);
            this.admissionControlledRequestsPerSec.RawValue = 0;

            this.admissionControlledRequests = new PerformanceCounter(this.performanceCategory, "Admission Controlled Requests", false);
            this.admissionControlledRequests.RawValue = 0;

            // Written by TransportClient and HttpTransportClient: back end request flow.
            this.backendRequestsPerSec = new PerformanceCounter(this.performanceCategory, "Backend Requests/sec", false);
            this.backendRequestsPerSec.RawValue = 0;

            this.backendActiveRequests = new PerformanceCounter(this.performanceCategory, "Backend Active Requests", false);
            this.backendActiveRequests.RawValue = 0;

            this.routingFailures = new PerformanceCounter(this.performanceCategory, "Routing Failures", false);
            this.routingFailures.RawValue = 0;

            // Written by PerformanceActivities: query and stored procedure timings, and back end connect latency.
            this.queryRequestsPerSec = new PerformanceCounter(this.performanceCategory, "Query Requests/sec", false);
            this.queryRequestsPerSec.RawValue = 0;

            this.procedureRequestsPerSec = new PerformanceCounter(this.performanceCategory, "Procedure Requests/sec", false);
            this.procedureRequestsPerSec.RawValue = 0;

            this.averageQueryRequestsDuration = new PerformanceCounter(this.performanceCategory, "Average Query Requests Duration", false);
            this.averageQueryRequestsDuration.RawValue = 0;

            this.averageQueryRequestsDurationBase = new PerformanceCounter(this.performanceCategory, "Average Query Requests Duration Base", false);
            this.averageQueryRequestsDurationBase.RawValue = 0;

            this.averageProcedureRequestsDuration = new PerformanceCounter(this.performanceCategory, "Average Procedure Requests Duration", false);
            this.averageProcedureRequestsDuration.RawValue = 0;

            this.averageProcedureRequestsDurationBase = new PerformanceCounter(this.performanceCategory, "Average Procedure Requests Duration Base", false);
            this.averageProcedureRequestsDurationBase.RawValue = 0;

            this.backendConnectionOpenAverageLatency = new PerformanceCounter(this.performanceCategory, "Backend Connection Open Average Latency", false);
            this.backendConnectionOpenAverageLatency.RawValue = 0;

            this.backendConnectionOpenAverageLatencyBase = new PerformanceCounter(this.performanceCategory, "Backend Connection Open Average Latency Base", false);
            this.backendConnectionOpenAverageLatencyBase.RawValue = 0;

            // Written by Connection.
            this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond = new PerformanceCounter(this.performanceCategory, "Backend Connection Open Failures Due To Syn Retransmit Timeout/sec", false);
            this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond.RawValue = 0;

            // Not written by any caller today. They are installed and bound, so they exist in the
            // category and read as zero. Left in place because removing a counter changes the
            // category shape, which forces a delete and recreate on the next privileged setup.
            this.currentFrontendConnections = new PerformanceCounter(this.performanceCategory, "Current Frontend Connections", false);
            this.currentFrontendConnections.RawValue = 0;

            this.triggerRequestsPerSec = new PerformanceCounter(this.performanceCategory, "Trigger Requests/sec", false);
            this.triggerRequestsPerSec.RawValue = 0;
        }

        #region IDisposable Members

        public void Dispose()
        {
#pragma warning disable SA1501
            using (this.frontendActiveRequests) { }

            using (this.frontendRequestsPerSec) { }

            using (this.admissionControlledRequests) { }

            using (this.admissionControlledRequestsPerSec) { }

            using (this.backendActiveRequests) { }

            using (this.backendRequestsPerSec) { }

            using (this.currentFrontendConnections) { }

            using (this.queryRequestsPerSec) { }

            using (this.triggerRequestsPerSec) { }

            using (this.procedureRequestsPerSec) { }

            using (this.averageProcedureRequestsDuration) { }

            using (this.averageProcedureRequestsDurationBase) { }

            using (this.averageQueryRequestsDuration) { }

            using (this.averageQueryRequestsDurationBase) { }

            using (this.backendConnectionOpenAverageLatency) { }

            using (this.backendConnectionOpenAverageLatencyBase) { }

            using (this.routingFailures) { }

            using (this.backendConnectionOpenFailuresDueToSynRetransmitPerSecond) { }
#pragma warning restore SA1501
        }

        #endregion
    }
}
