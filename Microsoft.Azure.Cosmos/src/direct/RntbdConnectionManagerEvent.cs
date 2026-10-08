//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Typed event categories for the <c>RntbdConnectionManagerEvent</c>
    /// Kusto table. Surfaced as string keys in the Kusto row so consumers
    /// don't depend on enum ordinals.
    /// </summary>
    internal enum RntbdConnectionManagerEventType
    {
        ManagerCreated,
        ManagerDisposed,
        OpenStarted,
        OpenCompleted,
        OpenFailed,
        SlotFreed,
        PreWarmTriggered,
        PoolSnapshot,
        RouteDecision,
    }

    /// <summary>
    /// Which transport path a request was routed down at the
    /// <c>CMTransportClient.GetChannel</c> fork. Surfaced on
    /// <see cref="RntbdConnectionManagerEvent.RoutePath"/>.
    /// </summary>
    internal enum RntbdRoutePath
    {
        Legacy,
        Manager,
    }

    /// <summary>
    /// Reason an opener task was triggered. Surfaced on
    /// <see cref="RntbdConnectionManagerEvent.TriggerReason"/>.
    /// </summary>
    internal enum OpenTrigger
    {
        OnDemandAcquire,
        PreWarm,
        ReplicaValidation,
        IdleRevalidation,
    }

    /// <summary>
    /// Reason a slot's connection was freed. Surfaced on
    /// <see cref="RntbdConnectionManagerEvent.FreeReason"/>.
    /// </summary>
    internal enum SlotFreeReason
    {
        Unhealthy,
        Idle,
        Disposal,
    }

    /// <summary>
    /// Stage at which an open attempt failed. Surfaced on
    /// <see cref="RntbdConnectionManagerEvent.FailedStage"/>.
    /// </summary>
    internal enum OpenFailureStage
    {
        Unknown,
        TcpConnect,
        Tls,
        RntbdContext,
    }

    /// <summary>
    /// Per-manager Kusto telemetry row. Captures one manager-internal
    /// decision; independent of any single request.
    /// </summary>
    /// <remarks>
    /// Different event types populate different fields. Unused fields are
    /// nullable / zero. Only populated fields are emitted by
    /// <see cref="ToTelemetryLine"/>.
    /// </remarks>
    internal sealed class RntbdConnectionManagerEvent
    {
        public Guid ConnectionManagerId { get; }
        public RntbdConnectionManagerEventType EventType { get; }
        public string ServerEndpoint { get; }
        public DateTime Timestamp { get; }

        // Caller-supplied correlation id. Currently populated by
        // PreWarmTriggered (from the WarmAsync activityId) so the row can
        // be joined with downstream replica-validation traces.
        public Guid? ActivityId { get; }

        // ManagerCreated
        public int MaxChannels { get; }
        public int MaxRequestsPerChannel { get; }

        // ManagerDisposed
        public long? LifetimeMs { get; }
        public long? LifetimeAcquires { get; }
        public long? LifetimeOpens { get; }

        // OpenStarted / OpenCompleted / OpenFailed / SlotFreed / PreWarmTriggered
        public int? SlotIndex { get; }
        public Guid? ChannelId { get; }
        public OpenTrigger? TriggerReason { get; }

        // OpenCompleted / OpenFailed
        public long? TotalOpenDurationMs { get; }
        public long? TcpConnectDurationMs { get; }
        public long? TlsHandshakeDurationMs { get; }
        public long? RntbdContextDurationMs { get; }

        // OpenFailed
        public OpenFailureStage? FailedStage { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        // SlotFreed
        public SlotFreeReason? FreeReason { get; }
        public long? ChannelLifetimeMs { get; }
        public long? RequestsServed { get; }

        // PoolSnapshot
        public int? OpenConnectionCount { get; }
        public int? OpeningConnectionCount { get; }
        public int? ClosedConnectionCount { get; }
        public int? SlotsAvailable { get; }

        // RouteDecision (one row per request at the CMTransportClient fork;
        // ConnectionManagerId is empty, ServerEndpoint carries the target).
        // Debug-gated, per-request; Kusto counts these by path/bin so no
        // in-process accumulator is needed.
        public RntbdRoutePath? RoutePath { get; }
        public bool? LocalRegion { get; }

        private RntbdConnectionManagerEvent(
            Guid connectionManagerId,
            RntbdConnectionManagerEventType eventType,
            string serverEndpoint,
            int? slotIndex = null,
            Guid? channelId = null,
            OpenTrigger? triggerReason = null,
            long? totalOpenDurationMs = null,
            long? tcpConnectDurationMs = null,
            long? tlsHandshakeDurationMs = null,
            long? rntbdContextDurationMs = null,
            OpenFailureStage? failedStage = null,
            string errorCode = null,
            string errorMessage = null,
            SlotFreeReason? freeReason = null,
            long? channelLifetimeMs = null,
            long? requestsServed = null,
            int maxChannels = 0,
            int maxRequestsPerChannel = 0,
            long? lifetimeMs = null,
            long? lifetimeAcquires = null,
            long? lifetimeOpens = null,
            Guid? activityId = null,
            int? openConnectionCount = null,
            int? openingConnectionCount = null,
            int? closedConnectionCount = null,
            int? slotsAvailable = null,
            RntbdRoutePath? routePath = null,
            bool? localRegion = null)
        {
            this.ConnectionManagerId = connectionManagerId;
            this.EventType = eventType;
            this.ServerEndpoint = serverEndpoint;
            this.Timestamp = DateTime.UtcNow;
            this.ActivityId = activityId;
            this.SlotIndex = slotIndex;
            this.ChannelId = channelId;
            this.TriggerReason = triggerReason;
            this.TotalOpenDurationMs = totalOpenDurationMs;
            this.TcpConnectDurationMs = tcpConnectDurationMs;
            this.TlsHandshakeDurationMs = tlsHandshakeDurationMs;
            this.RntbdContextDurationMs = rntbdContextDurationMs;
            this.FailedStage = failedStage;
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
            this.FreeReason = freeReason;
            this.ChannelLifetimeMs = channelLifetimeMs;
            this.RequestsServed = requestsServed;
            this.MaxChannels = maxChannels;
            this.MaxRequestsPerChannel = maxRequestsPerChannel;
            this.LifetimeMs = lifetimeMs;
            this.LifetimeAcquires = lifetimeAcquires;
            this.LifetimeOpens = lifetimeOpens;
            this.OpenConnectionCount = openConnectionCount;
            this.OpeningConnectionCount = openingConnectionCount;
            this.ClosedConnectionCount = closedConnectionCount;
            this.SlotsAvailable = slotsAvailable;
            this.RoutePath = routePath;
            this.LocalRegion = localRegion;
        }

        public static RntbdConnectionManagerEvent ManagerCreated(
            Guid cm, string endpoint, int maxChannels, int maxRequestsPerChannel)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.ManagerCreated, endpoint,
                maxChannels: maxChannels, maxRequestsPerChannel: maxRequestsPerChannel);
        }

        public static RntbdConnectionManagerEvent ManagerDisposed(
            Guid cm, string endpoint, long lifetimeMs, long lifetimeAcquires, long lifetimeOpens)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.ManagerDisposed, endpoint,
                lifetimeMs: lifetimeMs,
                lifetimeAcquires: lifetimeAcquires,
                lifetimeOpens: lifetimeOpens);
        }

        public static RntbdConnectionManagerEvent OpenStarted(
            Guid cm, string endpoint, int slotIndex, Guid channelId, OpenTrigger triggerReason,
            Guid? openActivityId = null)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.OpenStarted, endpoint,
                slotIndex: slotIndex,
                channelId: channelId,
                triggerReason: triggerReason,
                activityId: openActivityId);
        }

        public static RntbdConnectionManagerEvent OpenCompleted(
            Guid cm, string endpoint, int slotIndex, Guid channelId,
            long totalDurationMs, long? tcpMs, long? tlsMs, long? rntbdMs,
            Guid? openActivityId = null)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.OpenCompleted, endpoint,
                slotIndex: slotIndex,
                channelId: channelId,
                totalOpenDurationMs: totalDurationMs,
                tcpConnectDurationMs: tcpMs,
                tlsHandshakeDurationMs: tlsMs,
                rntbdContextDurationMs: rntbdMs,
                activityId: openActivityId);
        }

        public static RntbdConnectionManagerEvent OpenFailed(
            Guid cm, string endpoint, int slotIndex, Guid channelId,
            long totalDurationMs, OpenFailureStage failedStage,
            string errorCode, string errorMessage,
            long? tcpMs = null, long? tlsMs = null, long? rntbdMs = null,
            Guid? openActivityId = null)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.OpenFailed, endpoint,
                slotIndex: slotIndex,
                channelId: channelId,
                totalOpenDurationMs: totalDurationMs,
                tcpConnectDurationMs: tcpMs,
                tlsHandshakeDurationMs: tlsMs,
                rntbdContextDurationMs: rntbdMs,
                failedStage: failedStage,
                errorCode: errorCode,
                errorMessage: errorMessage,
                activityId: openActivityId);
        }

        public static RntbdConnectionManagerEvent SlotFreed(
            Guid cm, string endpoint, int slotIndex, Guid channelId, SlotFreeReason reason,
            long channelLifetimeMs, long requestsServed)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.SlotFreed, endpoint,
                slotIndex: slotIndex,
                channelId: channelId,
                freeReason: reason,
                channelLifetimeMs: channelLifetimeMs,
                requestsServed: requestsServed);
        }

        public static RntbdConnectionManagerEvent PreWarmTriggered(
            Guid cm, string endpoint, OpenTrigger reason, int? targetSlotIndex, Guid activityId)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.PreWarmTriggered, endpoint,
                triggerReason: reason,
                slotIndex: targetSlotIndex,
                activityId: activityId);
        }

        /// <summary>
        /// Periodic per-manager heartbeat. Carries current pool composition
        /// for this endpoint only.
        /// </summary>
        public static RntbdConnectionManagerEvent PoolSnapshot(
            Guid cm, string endpoint,
            int openCount, int openingCount, int closedCount, int slotsAvailable,
            long lifetimeAcquires, long lifetimeOpens)
        {
            return new RntbdConnectionManagerEvent(
                cm, RntbdConnectionManagerEventType.PoolSnapshot, endpoint,
                lifetimeAcquires: lifetimeAcquires,
                lifetimeOpens: lifetimeOpens,
                openConnectionCount: openCount,
                openingConnectionCount: openingCount,
                closedConnectionCount: closedCount,
                slotsAvailable: slotsAvailable);
        }

        /// <summary>
        /// One row per request at the <c>CMTransportClient.GetChannel</c> fork,
        /// recording which transport path (manager vs legacy) the request was
        /// routed down. Debug-gated and per-request; dashboards count these rows
        /// by path/bin, so no in-process accumulator is required. Restart-safe
        /// and multi-instance-correct by construction (each row carries its own
        /// RoleInstance).
        /// </summary>
        public static RntbdConnectionManagerEvent RouteDecision(
            RntbdRoutePath path, string endpoint, bool localRegion, Guid activityId)
        {
            return new RntbdConnectionManagerEvent(
                Guid.Empty, RntbdConnectionManagerEventType.RouteDecision, endpoint,
                routePath: path,
                localRegion: localRegion,
                activityId: activityId);
        }

        /// <summary>
        /// Single-line key=value serialization. Only populated fields are
        /// emitted to keep the line short and parser-friendly.
        /// </summary>
        public string ToTelemetryLine()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(192);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "RntbdConnectionManagerEvent cm={0:N} type={1} server={2} ts={3:O}",
                this.ConnectionManagerId, this.EventType, this.ServerEndpoint ?? string.Empty, this.Timestamp);
            AppendIfSet(sb, "slot", this.SlotIndex);
            if (this.ChannelId.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " channel={0:N}", this.ChannelId.Value);
            }
            if (this.ActivityId.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " activityId={0:N}", this.ActivityId.Value);
            }
            AppendEnumIfSet(sb, "trigger", this.TriggerReason);
            AppendIfSet(sb, "totalMs", this.TotalOpenDurationMs);
            AppendIfSet(sb, "tcpMs", this.TcpConnectDurationMs);
            AppendIfSet(sb, "tlsMs", this.TlsHandshakeDurationMs);
            AppendIfSet(sb, "rntbdMs", this.RntbdContextDurationMs);
            AppendEnumIfSet(sb, "failedStage", this.FailedStage);
            AppendIfSet(sb, "errorCode", this.ErrorCode);
            AppendIfSet(sb, "errorMessage", this.ErrorMessage);
            AppendEnumIfSet(sb, "freeReason", this.FreeReason);
            AppendIfSet(sb, "channelLifetimeMs", this.ChannelLifetimeMs);
            AppendIfSet(sb, "requestsServed", this.RequestsServed);
            if (this.MaxChannels != 0)
            {
                AppendIfSet(sb, "maxChannels", (int?)this.MaxChannels);
                AppendIfSet(sb, "maxRequestsPerChannel", (int?)this.MaxRequestsPerChannel);
            }
            AppendIfSet(sb, "lifetimeMs", this.LifetimeMs);
            AppendIfSet(sb, "lifetimeAcquires", this.LifetimeAcquires);
            AppendIfSet(sb, "lifetimeOpens", this.LifetimeOpens);
            AppendIfSet(sb, "openConns", this.OpenConnectionCount);
            AppendIfSet(sb, "openingConns", this.OpeningConnectionCount);
            AppendIfSet(sb, "closedConns", this.ClosedConnectionCount);
            AppendIfSet(sb, "slotsAvailable", this.SlotsAvailable);
            AppendEnumIfSet(sb, "path", this.RoutePath);
            if (this.LocalRegion.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " localRegion={0}", this.LocalRegion.Value);
            }
            return sb.ToString();
        }

        private static void AppendIfSet(System.Text.StringBuilder sb, string key, int? value)
        {
            if (value.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1}", key, value.Value);
            }
        }

        private static void AppendIfSet(System.Text.StringBuilder sb, string key, long? value)
        {
            if (value.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1}", key, value.Value);
            }
        }

        private static void AppendIfSet(System.Text.StringBuilder sb, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1}", key, value);
            }
        }

        private static void AppendEnumIfSet<TEnum>(System.Text.StringBuilder sb, string key, TEnum? value)
            where TEnum : struct
        {
            if (value.HasValue)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0}={1}", key, value.Value);
            }
        }
    }
}
