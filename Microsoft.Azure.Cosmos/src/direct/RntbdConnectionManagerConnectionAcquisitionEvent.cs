//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Per-request Kusto telemetry row emitted once per
    /// <see cref="RntbdConnectionManager.AcquireAsync"/> call. Joined to
    /// <see cref="RntbdConnectionManagerEvent"/> via
    /// <see cref="ConnectionManagerId"/>.
    /// </summary>
    /// <remarks>
    /// Immutable value bag. Construct once at the end of the acquire path
    /// and hand to <see cref="IRntbdConnectionManagerTelemetry.LogAcquisition"/>.
    /// Default formatting emits a single grep-friendly line that downstream
    /// Kusto ingestion can parse.
    /// </remarks>
    internal sealed class RntbdConnectionManagerConnectionAcquisitionEvent
    {
        public Guid ConnectionManagerId { get; }
        public Guid ActivityId { get; }
        public string ServerEndpoint { get; }
        public bool LocalRegion { get; }
        public long WaitForConnectionMs { get; }
        public PoolState PoolStateAtEntry { get; }
        public bool JoinedExistingOpenerTask { get; }
        public bool TriggeredOpen { get; }
        public int EnsureOpenCalls { get; }
        public int StaleSlotsConsumed { get; }
        public AcquireOutcome Outcome { get; }
        public string ErrorCode { get; }

        public RntbdConnectionManagerConnectionAcquisitionEvent(
            Guid connectionManagerId,
            Guid activityId,
            string serverEndpoint,
            bool localRegion,
            long waitForConnectionMs,
            PoolState poolStateAtEntry,
            bool joinedExistingOpenerTask,
            bool triggeredOpen,
            int ensureOpenCalls,
            int staleSlotsConsumed,
            AcquireOutcome outcome,
            string errorCode = null)
        {
            this.ConnectionManagerId = connectionManagerId;
            this.ActivityId = activityId;
            this.ServerEndpoint = serverEndpoint;
            this.LocalRegion = localRegion;
            this.WaitForConnectionMs = waitForConnectionMs;
            this.PoolStateAtEntry = poolStateAtEntry;
            this.JoinedExistingOpenerTask = joinedExistingOpenerTask;
            this.TriggeredOpen = triggeredOpen;
            this.EnsureOpenCalls = ensureOpenCalls;
            this.StaleSlotsConsumed = staleSlotsConsumed;
            this.Outcome = outcome;
            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Single-line key=value serialization. Format is stable and used by
        /// downstream Kusto ingestion to parse rows out of the trace stream.
        /// </summary>
        public string ToTelemetryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "RntbdConnectionManagerConnectionAcquisition cm={0:N} activityId={1:N} server={2} localRegion={3} waitMs={4} poolState={5} joined={6} triggered={7} ensureOpenCalls={8} staleSlots={9} outcome={10} errorCode={11}",
                this.ConnectionManagerId,
                this.ActivityId,
                this.ServerEndpoint ?? string.Empty,
                this.LocalRegion,
                this.WaitForConnectionMs,
                this.PoolStateAtEntry,
                this.JoinedExistingOpenerTask,
                this.TriggeredOpen,
                this.EnsureOpenCalls,
                this.StaleSlotsConsumed,
                this.Outcome,
                this.ErrorCode ?? string.Empty);
        }
    }

    /// <summary>
    /// Outcome of an <see cref="RntbdConnectionManager.AcquireAsync"/> call.
    /// </summary>
    internal enum AcquireOutcome
    {
        /// <summary>The acquire returned an active connection.</summary>
        Acquired = 0,

        /// <summary>The caller's <see cref="System.Threading.CancellationToken"/> fired before a slot was available.</summary>
        CallerCancelled = 1,

        /// <summary>The manager was disposed while the acquire was in flight.</summary>
        ManagerDisposed = 2,

        /// <summary>The opener task this acquire raced against failed before producing a slot. Acquirer received the open exception.</summary>
        OpenFailed = 3,
    }

    /// <summary>
    /// Pool state observed at the start of an
    /// <see cref="RntbdConnectionManager.AcquireAsync"/> call. Used as
    /// the <see cref="RntbdConnectionManagerConnectionAcquisitionEvent.PoolStateAtEntry"/>
    /// field. Serialized via <see cref="System.Enum.ToString()"/>, which
    /// produces stable text identical to the enum member name — safe for
    /// downstream Kusto parsers.
    /// </summary>
    internal enum PoolState
    {
        /// <summary>The slot queue had no available items at acquire entry.</summary>
        Empty = 0,

        /// <summary>The slot queue had at least one available item at acquire entry.</summary>
        Open = 1,
    }
}
