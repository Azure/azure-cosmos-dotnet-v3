//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Reason the SDK chose to dispatch a request to a particular region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This enum is non-exhaustive.</b> Additional values may be added in future SDK
    /// versions to describe new dispatch reasons. Callers that switch on a
    /// <see cref="RequestedRegionReason"/> value MUST include a <c>default</c> arm
    /// to handle unknown values gracefully, for example:
    /// </para>
    /// <code language="c#">
    /// <![CDATA[
    /// switch (region.Reason)
    /// {
    ///     case RequestedRegionReason.Initial:          /* ... */ break;
    ///     case RequestedRegionReason.OperationRetry:   /* ... */ break;
    ///     case RequestedRegionReason.Hedging:          /* ... */ break;
    ///     case RequestedRegionReason.RegionFailover:   /* ... */ break;
    ///     case RequestedRegionReason.CircuitBreakerProbe: /* ... */ break;
    ///     case RequestedRegionReason.TransportRetry:   /* ... */ break;
    ///     default:
    ///         // Future-proof: an unknown reason was added in a newer SDK.
    ///         break;
    /// }
    /// ]]>
    /// </code>
    /// <para>
    /// Cross-SDK note: not every reason is populated by every SDK in every release. In the
    /// .NET SDK, <see cref="TransportRetry"/> is reserved but not emitted from the SDK
    /// layer in this version because the transport retry decision is owned by the closed-source
    /// <c>Microsoft.Azure.Cosmos.Direct</c> package and is not observable from the SDK layer.
    /// </para>
    /// </remarks>
    public enum RequestedRegionReason : byte
    {
        /// <summary>
        /// The first dispatch of the operation. Appears exactly once per operation at the
        /// beginning of the dispatch sequence.
        /// </summary>
        Initial = 0,

        /// <summary>
        /// An operation-level retry decided by the SDK's client-retry policy (e.g. retry
        /// after 410 Gone, 449 Conflict-on-Write, or throttling). The retry targets the
        /// same region as the previous attempt.
        /// </summary>
        OperationRetry = 1,

        /// <summary>
        /// A transport-level retry inside the per-region transport stack (e.g. TCP reset
        /// or single-frame failure). <b>Reserved but not populated by the .NET SDK in this
        /// release.</b> Transport retries are issued by the closed-source
        /// <c>Microsoft.Azure.Cosmos.Direct</c> package and are not observable from the
        /// public SDK layer.
        /// </summary>
        TransportRetry = 2,

        /// <summary>
        /// A speculative cross-region fan-out dispatch issued by the configured
        /// <see cref="AvailabilityStrategy"/>. The primary (first) dispatch is recorded
        /// as <see cref="Initial"/>; only the additional hedge arms are tagged as
        /// <see cref="Hedging"/>.
        /// </summary>
        Hedging = 3,

        /// <summary>
        /// An endpoint-failure-driven retry to a different region (write conflict re-route,
        /// region marked unavailable). The SDK has marked the previous endpoint as failed
        /// for this operation and chosen a different region from the preferred-locations list.
        /// </summary>
        RegionFailover = 4,

        /// <summary>
        /// A probe dispatch to a previously circuit-broken region driven by the SDK's
        /// per-partition automatic failover / per-partition per-region circuit breaker
        /// (PPCB) health check.
        /// </summary>
        CircuitBreakerProbe = 5,
    }
}
