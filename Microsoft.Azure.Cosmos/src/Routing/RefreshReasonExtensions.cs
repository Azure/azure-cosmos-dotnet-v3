//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Wire-format helpers for <see cref="RefreshReason"/>.
    ///
    /// The dictionary <see cref="WireValues"/> is the single source of truth
    /// for mapping an enum value to its header wire value. Every member of
    /// <see cref="RefreshReason"/> MUST have an entry here; this is enforced
    /// by unit tests (enum-coverage test).
    /// </summary>
    internal static class RefreshReasonExtensions
    {
        /// <summary>
        /// Maps each <see cref="RefreshReason"/> enum member to the exact
        /// literal string emitted on the wire in the
        /// <c>x-ms-cosmos-refresh-reason</c> header.
        ///
        /// Wire values are two dot-segments at most. Do not add a third
        /// segment or substring interpolation; values must be known at
        /// design time.
        /// </summary>
        public static readonly IReadOnlyDictionary<RefreshReason, string> WireValues =
            new Dictionary<RefreshReason, string>
            {
                { RefreshReason.Unspecified, "unspecified" },
                { RefreshReason.GoneServer, "gone.server" },
                { RefreshReason.GoneCompletingSplit, "gone.completing_split" },
                { RefreshReason.GoneCompletingPartitionMigration, "gone.completing_partition_migration" },
                { RefreshReason.GoneNameCacheStale, "gone.name_cache_stale" },
                { RefreshReason.GonePartitionKeyRangeGone, "gone.partition_key_range_gone" },
                { RefreshReason.GoneUnknown, "gone.unknown" },
                { RefreshReason.GoneDnsResolution, "gone.dns_resolution" },
                { RefreshReason.GoneConnect, "gone.connect" },
                { RefreshReason.GoneSslNegotiation, "gone.ssl_negotiation" },
                { RefreshReason.GoneNegotiationTimeout, "gone.negotiation_timeout" },
                { RefreshReason.GoneChannelMultiplexerClosed, "gone.channel_multiplexer_closed" },
                { RefreshReason.GoneSend, "gone.send" },
                { RefreshReason.GoneSendLockTimeout, "gone.send_lock_timeout" },
                { RefreshReason.GoneReceive, "gone.receive" },
                { RefreshReason.GoneReceiveStreamClosed, "gone.receive_stream_closed" },
                { RefreshReason.GoneConnectionBroken, "gone.connection_broken" },
                { RefreshReason.GoneChannelWaitingToOpenTimeout, "gone.channel_waiting_to_open_timeout" },
                { RefreshReason.GoneWriteNotSent, "gone.write_not_sent" },
                { RefreshReason.InsufficientReplicasQuorum, "InsufficientReplicas.Quorum" },
                { RefreshReason.InsufficientReplicasSuboptimalTimer, "InsufficientReplicas.SuboptimalTimer" },
                { RefreshReason.ReplicaHealthUnhealthyLongLived, "ReplicaHealth.unhealthyLongLived" },
                { RefreshReason.ConnectionEventServerClosed, "connection_event.server_closed" },
            };

        /// <summary>
        /// Returns the wire string for the given reason. Throws if the enum
        /// value is missing from <see cref="WireValues"/> — this is a design
        /// invariant enforced by tests.
        /// </summary>
        public static string ToHeaderValue(this RefreshReason reason)
        {
            if (WireValues.TryGetValue(reason, out string value))
            {
                return value;
            }

            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                $"No wire value registered for RefreshReason.{reason}. Add an entry in RefreshReasonExtensions.WireValues.");
        }

        /// <summary>
        /// Maps a <see cref="TransportErrorCode"/> (from an inner
        /// <c>TransportException</c> inside a Gone) to the most specific
        /// <see cref="RefreshReason"/>.
        ///
        /// Codes explicitly documented as generic/default catch-alls
        /// (<see cref="TransportErrorCode.Unknown"/>,
        /// <see cref="TransportErrorCode.ChannelOpenFailed"/>,
        /// <see cref="TransportErrorCode.ChannelOpenTimeout"/>,
        /// <see cref="TransportErrorCode.RequestTimeout"/>) fold into
        /// <see cref="RefreshReason.GoneUnknown"/>.
        /// </summary>
        public static RefreshReason FromTransportErrorCode(TransportErrorCode code)
        {
            switch (code)
            {
                case TransportErrorCode.Unknown:
                case TransportErrorCode.ChannelOpenFailed:
                case TransportErrorCode.ChannelOpenTimeout:
                case TransportErrorCode.RequestTimeout:
                    return RefreshReason.GoneUnknown;

                case TransportErrorCode.DnsResolutionFailed:
                case TransportErrorCode.DnsResolutionTimeout:
                    return RefreshReason.GoneDnsResolution;

                case TransportErrorCode.ConnectFailed:
                case TransportErrorCode.ConnectTimeout:
                    return RefreshReason.GoneConnect;

                case TransportErrorCode.SslNegotiationFailed:
                case TransportErrorCode.SslNegotiationTimeout:
                    return RefreshReason.GoneSslNegotiation;

                case TransportErrorCode.TransportNegotiationTimeout:
                    return RefreshReason.GoneNegotiationTimeout;

                case TransportErrorCode.ChannelMultiplexerClosed:
                    return RefreshReason.GoneChannelMultiplexerClosed;

                case TransportErrorCode.SendFailed:
                case TransportErrorCode.SendTimeout:
                    return RefreshReason.GoneSend;

                case TransportErrorCode.SendLockTimeout:
                    return RefreshReason.GoneSendLockTimeout;

                case TransportErrorCode.ReceiveFailed:
                case TransportErrorCode.ReceiveTimeout:
                    return RefreshReason.GoneReceive;

                case TransportErrorCode.ReceiveStreamClosed:
                    return RefreshReason.GoneReceiveStreamClosed;

                case TransportErrorCode.ConnectionBroken:
                    return RefreshReason.GoneConnectionBroken;

                case TransportErrorCode.ChannelWaitingToOpenTimeout:
                    return RefreshReason.GoneChannelWaitingToOpenTimeout;

                default:
                    // A new TransportErrorCode was added upstream without
                    // updating this switch. Fall back to GoneUnknown so the
                    // gateway still gets *a* reason; the exhaustive test in
                    // RefreshReasonFormatterTests will fail and prompt a fix.
                    return RefreshReason.GoneUnknown;
            }
        }

        /// <summary>
        /// Classifies a Gone surfaced on a prior <see cref="StoreResult"/> into
        /// the most specific <see cref="RefreshReason"/>. Called by
        /// <c>StoreReader</c> right before it flips
        /// <c>ForceRefreshAddressCache</c> on the retry, so the outgoing
        /// /addresses request carries the originating cause.
        /// Preference order: inner <c>TransportException</c> (transport-synth
        /// 410) &gt; server substatus &gt; <see cref="RefreshReason.GoneServer"/>.
        /// </summary>
        public static RefreshReason ClassifyGoneFromException(Exception exception, SubStatusCodes subStatusCode)
        {
            // Walk inner-exception chain for a TransportException; transport-
            // synth 410 always has one.
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is TransportException transportException)
                {
                    return FromTransportErrorCode(transportException.ErrorCode);
                }
            }

            switch (subStatusCode)
            {
                case SubStatusCodes.CompletingSplit:
                    return RefreshReason.GoneCompletingSplit;
                case SubStatusCodes.CompletingPartitionMigration:
                    return RefreshReason.GoneCompletingPartitionMigration;
                case SubStatusCodes.NameCacheIsStale:
                    return RefreshReason.GoneNameCacheStale;
                case SubStatusCodes.PartitionKeyRangeGone:
                    return RefreshReason.GonePartitionKeyRangeGone;
                default:
                    return RefreshReason.GoneServer;
            }
        }
    }
}
