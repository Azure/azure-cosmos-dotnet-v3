//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    /// <summary>
    /// A design-time-bounded, closed enumeration of reasons the SDK performs a
    /// forced cache refresh. The value is emitted on the wire as the
    /// <c>x-ms-cosmos-refresh-reason</c> header so the service can attribute
    /// each forced refresh to its true cause.
    ///
    /// The enum is deliberately GENERIC (not address-cache specific). Existing
    /// values correspond to address-cache force-refresh paths. Future values
    /// may be added for other forced-refresh egress paths (partition-key-range
    /// cache ChangeFeed forward, collection-routing-map refresh, etc.).
    ///
    /// Naming convention for wire values: two dot-separated segments,
    /// <c>&lt;cache_or_surface&gt;.&lt;subcause&gt;</c>. Never three segments.
    ///
    /// When adding a new value:
    /// - Give it an explicit integer value. Do not reuse retired values.
    /// - Add an entry in <see cref="RefreshReasonExtensions.WireValues"/>.
    /// - If it is driven by a <c>TransportErrorCode</c>, wire it into
    ///   <see cref="RefreshReasonExtensions.FromTransportErrorCode"/>.
    /// </summary>
    internal enum RefreshReason
    {
        // Sentinel. Must never appear on the wire in production once all call
        // sites are tagged. The opt-in validator enforces this in tests.
        Unspecified = 0,

        // ---- Group A: real 410 from the server (no transport synthesis) ----
        GoneServer = 1,

        // ---- Group B: Gone with server-provided substatus (routing-topology changes) ----
        // NOTE: these typically drive a PK-range / collection-cache refresh
        // rather than an address-cache refresh; they are pre-positioned here
        // because the enum is generic and will tag PK-range egress too.
        GoneCompletingSplit = 2,
        GoneCompletingPartitionMigration = 3,
        GoneNameCacheStale = 4,
        GonePartitionKeyRangeGone = 5,

        // ---- Group C: Gone synthesized by the SDK's transport layer ----
        // Pairs of (*Failed, *Timeout) are intentionally collapsed because the
        // gateway's reaction is the same.
        GoneUnknown = 6, // TransportErrorCode.Unknown, ChannelOpenFailed, ChannelOpenTimeout, RequestTimeout
        GoneDnsResolution = 7, // DnsResolutionFailed, DnsResolutionTimeout
        GoneConnect = 8, // ConnectFailed, ConnectTimeout
        GoneSslNegotiation = 9, // SslNegotiationFailed, SslNegotiationTimeout
        GoneNegotiationTimeout = 10, // TransportNegotiationTimeout
        GoneChannelMultiplexerClosed = 11, // ChannelMultiplexerClosed
        GoneSend = 12, // SendFailed, SendTimeout
        GoneSendLockTimeout = 13, // SendLockTimeout (client-side lock contention)
        GoneReceive = 14, // ReceiveFailed, ReceiveTimeout
        GoneReceiveStreamClosed = 15, // ReceiveStreamClosed (server clean close while awaiting response)
        GoneConnectionBroken = 16, // ConnectionBroken
        GoneChannelWaitingToOpenTimeout = 17, // ChannelWaitingToOpenTimeout (slot-wait saturation)
        GoneWriteNotSent = 18, // DocumentServiceRequest.UserRequestSent == false on write-path Gone synthesis

        // ---- Group D: forced refresh NOT driven by a Gone ----
        InsufficientReplicasQuorum = 19, // StoreReader decided replica-set too small for consistency
        InsufficientReplicasSuboptimalTimer = 20, // 10-minute suboptimal-replica-set timer
        ReplicaHealthUnhealthyLongLived = 21, // on-demand revalidation of a URI unhealthy >= 1 minute
        ConnectionEventServerClosed = 22, // Dispatcher.RaiseConnectionEvent -> ReadEof / ReadFailure

        // Insert new values above this comment with the next integer value.
    }
}
