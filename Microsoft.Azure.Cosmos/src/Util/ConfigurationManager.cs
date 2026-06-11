//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class ConfigurationManager
    {
        /// <summary>
        /// A read-only string containing the environment variable name for enabling replica validation.
        /// This will eventually be removed once replica valdiatin is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string ReplicaConnectivityValidationEnabled = "AZURE_COSMOS_REPLICA_VALIDATION_ENABLED";

        /// <summary>
        /// A read-only string containing the environment variable name for enabling per partition automatic failover.
        /// This will eventually be removed once per partition automatic failover is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string PartitionLevelFailoverEnabled = "AZURE_COSMOS_PARTITION_LEVEL_FAILOVER_ENABLED";

        /// <summary>
        /// A read-only string containing the environment variable name for enabling per partition circuit breaker. The default value
        /// for this flag is false.
        /// </summary>
        internal static readonly string PartitionLevelCircuitBreakerEnabled = "AZURE_COSMOS_CIRCUIT_BREAKER_ENABLED";

        /// <summary>
        /// A read-only string containing the environment variable name for capturing the stale partition refresh task interval time
        /// in seconds. The default value for this interval is 60 seconds.
        /// </summary>
        internal static readonly string StalePartitionUnavailabilityRefreshIntervalInSeconds = "AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS";

        /// <summary>
        /// A read-only string containing the environment variable name for capturing the unavailability duration applicable for a failed partition
        /// before the partition can be considered for a refresh by the background task.
        /// </summary>
        internal static readonly string AllowedPartitionUnavailabilityDurationInSeconds = "AZURE_COSMOS_PPCB_ALLOWED_PARTITION_UNAVAILABILITY_DURATION_IN_SECONDS";

        /// <summary>
        /// Environment variable name to enable thin client mode.
        /// </summary>
        internal static readonly string ThinClientModeEnabled = "AZURE_COSMOS_THIN_CLIENT_ENABLED";

        /// <summary>
        /// Environment variable to override AAD scope.
        /// </summary>
        internal static readonly string AADScopeOverride = "AZURE_COSMOS_AAD_SCOPE_OVERRIDE";

        /// <summary>
        /// A read-only string containing the environment variable name for capturing the consecutive failure count for reads, before triggering per partition
        /// circuit breaker flow. The default value for this interval is 10 consecutive requests within 1 min window.
        /// </summary>
        internal static readonly string CircuitBreakerConsecutiveFailureCountForReads = "AZURE_COSMOS_PPCB_CONSECUTIVE_FAILURE_COUNT_FOR_READS";

        /// <summary>
        /// A read-only string containing the environment variable name for capturing the consecutive failure count for writes, before triggering per partition
        /// circuit breaker flow. The default value for this interval is 10 consecutive requests within 1 min window.
        /// </summary>
        internal static readonly string CircuitBreakerConsecutiveFailureCountForWrites = "AZURE_COSMOS_PPCB_CONSECUTIVE_FAILURE_COUNT_FOR_WRITES";

        /// <summary>
        /// A read-only string containing the environment variable name for capturing the consecutive failure count for writes, before triggering per partition
        /// circuit breaker flow. The default value for this interval is 5 consecutive requests within 1 min window.
        /// </summary>
        internal static readonly string CircuitBreakerTimeoutCounterResetWindowInMinutes = "AZURE_COSMOS_PPCB_TIMEOUT_COUNTER_RESET_WINDOW_IN_MINUTES";

        /// <summary>
        /// Environment variable name for overriding optimistic direct execution of queries.
        /// </summary>
        internal static readonly string OptimisticDirectExecutionEnabled = "AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED";

        /// <summary>
        /// Environment variable name to disable sending non streaming order by query feature flag to the gateway.
        /// </summary>
        internal static readonly string HybridSearchQueryPlanOptimizationDisabled = "AZURE_COSMOS_HYBRID_SEARCH_QUERYPLAN_OPTIMIZATION_DISABLED";

        /// <summary>
        /// A read-only string containing the environment variable name for enabling hub region processing for read requests.
        /// When enabled (default), the SDK attaches a hub region header to read requests that encounter repeated 404/1002
        /// (ReadSessionNotAvailable) errors, allowing the hub region to process the request directly. When disabled, the
        /// SDK falls back to the original retry behavior without hub region header attachment.
        /// </summary>
        internal static readonly string HubRegionProcessingEnabled = "AZURE_COSMOS_HUB_REGION_PROCESSING_ENABLED";

        /// <summary>
        /// Environment variable name to enable distributed query gateway mode.
        /// </summary>
        internal static readonly string DistributedQueryGatewayModeEnabled = "AZURE_COSMOS_DISTRIBUTED_QUERY_GATEWAY_ENABLED";

        /// <summary>
        /// intent is If a client specify a value, we will force it to be atleast 100ms, otherwise default is going to be 500ms
        /// </summary>
        internal static readonly string MinInRegionRetryTimeForWritesInMs = "AZURE_COSMOS_SESSION_TOKEN_MISMATCH_IN_REGION_RETRY_TIME_IN_MILLISECONDS";
        internal static readonly int DefaultMinInRegionRetryTimeForWritesInMs = 500;
        internal static readonly int MinMinInRegionRetryTimeForWritesInMs = 100;

        /// <summary>
        /// intent is If a client specify a value, we will force it to be atleast 1, otherwise default is going to be 1(right now both the values are 1 but we have the provision to change them in future).
        /// </summary>
        internal static readonly string MaxRetriesInLocalRegionWhenRemoteRegionPreferred = "AZURE_COSMOS_MAX_RETRIES_IN_LOCAL_REGION_WHEN_REMOTE_REGION_PREFERRED";
        internal static readonly int DefaultMaxRetriesInLocalRegionWhenRemoteRegionPreferred = 1;
        internal static readonly int MinMaxRetriesInLocalRegionWhenRemoteRegionPreferred = 1;

        /// <summary>
        /// A read-only string containing the environment variable name for enabling binary encoding. This will eventually
        /// be removed once binary encoding is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string BinaryEncodingEnabled = "AZURE_COSMOS_BINARY_ENCODING_ENABLED";

        /// <summary>
        /// A read-only string containing the environment variable name for enabling binary encoding. This will eventually
        /// be removed once binary encoding is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string TcpChannelMultiplexingEnabled = "AZURE_COSMOS_TCP_CHANNEL_MULTIPLEX_ENABLED";

        /// <summary>
        /// A read-only string containing the environment variable name for bypassing query parsing.
        /// and GA.
        /// </summary>
        internal static readonly string BypassQueryParsing = "AZURE_COSMOS_BYPASS_QUERY_PARSING";

        /// <summary>
        /// A read-only string containing the environment variable name for disabling length aware range comparator.
        /// Length aware range comparators were intorduced in Range class to handle EPK range comparisons correctly in the case of a container's physical partition set consisting of fully and partially specified EPK values.
        /// By default length aware range comparator is enabled. Refer to Range.cs in Msdata project for more details. Range.LengthAwareMinComparer/LengthAwareMaxComparer.
        /// Setting the value to false will disable length aware range comparator and switch to using the regular Range.MinComparer/MaxComparer.
        /// </summary>
        internal static readonly string UseLengthAwareRangeComparator = "AZURE_COSMOS_USE_LENGTH_AWARE_RANGE_COMPARATOR";

        /// <summary>
        /// Environment variable name to enable DNS dot-suffix (FQDN trailing dot) for
        /// Direct mode TCP connections. When enabled, appends a trailing '.' to hostnames
        /// before DNS resolution to bypass Kubernetes ndots search-domain expansion.
        /// See: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5730
        /// </summary>
        internal static readonly string TcpDnsDotSuffixEnabled = "AZURE_COSMOS_TCP_DNS_DOT_SUFFIX_ENABLED";

        /// <summary>
        /// Environment variable to override the HTTP/2 PING keep-alive delay (in seconds).
        /// After this many seconds of inactivity on an HTTP/2 connection, a PING frame is sent
        /// to detect broken connections in the pool. Default: 1 second.
        /// </summary>
        internal static readonly string Http2KeepAlivePingDelayInSeconds = "AZURE_COSMOS_HTTP2_KEEPALIVE_PING_DELAY_IN_SECONDS";

        /// <summary>
        /// Environment variable to override the HTTP/2 PING keep-alive timeout (in seconds).
        /// If no PONG response is received within this time, the connection is marked dead. Default: 2 seconds.
        /// </summary>
        internal static readonly string Http2KeepAlivePingTimeoutInSeconds = "AZURE_COSMOS_HTTP2_KEEPALIVE_PING_TIMEOUT_IN_SECONDS";

        /// <summary>
        /// Environment variable name to enable deterministic lease-id partition key values for Change Feed lease creation.
        /// </summary>
        internal static readonly string ChangeFeedLeaseIdAsPartitionKeyEnabled = "AZURE_COSMOS_CHANGE_FEED_LEASE_ID_AS_PARTITION_KEY_ENABLED";

        /// <summary>
        /// Environment variable to override the SDK-internal hard deadline (in seconds) bounding the
        /// detached metadata-read operation in <see cref="MetadataDetachedExecutor"/>. This is a defensive
        /// upper bound on background work in the metadata-cache path; it is independent of the caller's
        /// CancellationToken. Default is 300 seconds (5 minutes).
        /// </summary>
        internal static readonly string MetadataDetachedHardDeadlineInSeconds = "AZURE_COSMOS_METADATA_DETACHED_HARD_DEADLINE_SECONDS";

        /// <summary>
        /// Default hard deadline for the detached metadata-read executor. Derivation:
        /// the executor wraps <c>ClientCollectionCache.ReadCollectionAsync</c>
        /// (collection metadata reads), which per <c>HttpTimeoutPolicy.GetTimeoutPolicy</c>
        /// (the <c>IsMetaData &amp;&amp; IsReadOnlyRequest</c> branch) routes to
        /// <c>HttpTimeoutPolicyControlPlaneRetriableHotPath</c> with ladder
        /// (1 s, 0) → (5 s, 1 s) → (65 s, 0) — 71 s of timeouts plus 1 s inter-attempt
        /// delay ≈ 72 s/region. A typical cross-region failover sweep visits ~3-5 regions
        /// before settling, so ~3-5 × 72 ≈ 215 s to 360 s of wall time, plus
        /// <c>ClientRetryPolicy.RetryIntervalInMS = 1000 ms</c> per failover. 300 s covers
        /// the common-case multi-region failover with margin. This is NOT a tight bound on
        /// <c>ClientRetryPolicy.MaxRetryCount = 120</c> — pathological failover ping-pong is
        /// bounded by <see cref="MetadataDetachedExecutor.MaxAttemptsHardCap"/> and by the
        /// policy's own counter; the time deadline targets realistic failover duration,
        /// not the theoretical worst case.
        /// Note: account reads via <c>GatewayAccountReader</c> use the slower
        /// <c>HttpTimeoutPolicyControlPlaneRead</c> ladder (5+10+20 = 35 s/region) instead,
        /// but the executor does not wrap that path today.
        /// </summary>
        internal static readonly int DefaultMetadataDetachedHardDeadlineInSeconds = 300;

        /// <summary>
        /// Lower bound (in seconds) clamped onto user-supplied <see cref="MetadataDetachedHardDeadlineInSeconds"/>
        /// values to prevent pathologically short deadlines from defeating the cross-region failover.
        /// </summary>
        internal static readonly int MinMetadataDetachedHardDeadlineInSeconds = 30;

        /// <summary>
        /// Upper bound (in seconds) clamped onto user-supplied <see cref="MetadataDetachedHardDeadlineInSeconds"/>
        /// values. Two reasons for this cap:
        /// <list type="bullet">
        ///   <item>The <c>CancellationTokenSource</c> constructor that takes a <see cref="TimeSpan"/>
        ///   throws <see cref="ArgumentOutOfRangeException"/> when the delay exceeds <c>uint.MaxValue - 1</c>
        ///   milliseconds (~49.7 days). An unbounded user value would break every metadata-cache read.</item>
        ///   <item>No legitimate metadata-read budget exceeds 24 hours; a value larger than this
        ///   indicates a misconfiguration that we should clamp and trace rather than honor.</item>
        /// </list>
        /// 86400 seconds (24 h) is ~288× the documented 300 s default and far beyond any realistic
        /// cross-region failover sequence.
        /// </summary>
        internal static readonly int MaxMetadataDetachedHardDeadlineInSeconds = 86400;

        public static T GetEnvironmentVariable<T>(string variable, T defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static int GetMaxRetriesInLocalRegionWhenRemoteRegionPreferred()
        {
            return Math.Max(
                ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: MaxRetriesInLocalRegionWhenRemoteRegionPreferred,
                        defaultValue: DefaultMaxRetriesInLocalRegionWhenRemoteRegionPreferred),
                MinMaxRetriesInLocalRegionWhenRemoteRegionPreferred);
        }

        public static TimeSpan GetMinRetryTimeInLocalRegionWhenRemoteRegionPreferred()
        {
            return TimeSpan.FromMilliseconds(Math.Max(
                ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: MinInRegionRetryTimeForWritesInMs,
                        defaultValue: DefaultMinInRegionRetryTimeForWritesInMs),
                MinMinInRegionRetryTimeForWritesInMs));
        }

        /// <summary>
        /// Gets the boolean value of the replica validation environment variable. Note that, replica validation
        /// is enabled by default for the preview package and disabled for GA at the moment. The user can set the
        /// respective environment variable 'AZURE_COSMOS_REPLICA_VALIDATION_ENABLED' to override the value for
        /// both preview and GA. The method will eventually be removed, once replica valdiatin is enabled by default
        /// for  both preview and GA.
        /// </summary>
        /// <param name="connectionPolicy">An instance of <see cref="ConnectionPolicy"/> containing the client options.</param>
        /// <returns>A boolean flag indicating if replica validation is enabled.</returns>
        public static bool IsReplicaAddressValidationEnabled(
            ConnectionPolicy connectionPolicy)
        {
            if (connectionPolicy != null
                && connectionPolicy.EnableAdvancedReplicaSelectionForTcp.HasValue)
            {
                return connectionPolicy.EnableAdvancedReplicaSelectionForTcp.Value;
            }

            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.ReplicaConnectivityValidationEnabled,
                        defaultValue: true);
        }

        /// <summary>
        /// Gets the boolean value indicating whether the thin client mode is enabled based on the environment variable override.
        /// </summary>
        /// <param name="defaultValue">A boolean field containing the default value for thin client mode.</param>
        /// <returns>A boolean flag indicating if thin client mode is enabled.</returns>
        public static bool IsThinClientEnabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.ThinClientModeEnabled,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating whether Change Feed lease creation should use lease id as the partition key value.
        /// </summary>
        /// <returns>A boolean flag indicating if deterministic lease-id partition key behavior is enabled.</returns>
        public static bool IsChangeFeedLeaseIdAsPartitionKeyEnabled()
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.ChangeFeedLeaseIdAsPartitionKeyEnabled,
                        defaultValue: true);
        }

        /// <summary>
        /// Returns the SDK-internal hard deadline used by <see cref="MetadataDetachedExecutor"/> to bound
        /// detached metadata-read operations. Reads <see cref="MetadataDetachedHardDeadlineInSeconds"/>
        /// from the environment and clamps it into the closed interval
        /// [<see cref="MinMetadataDetachedHardDeadlineInSeconds"/>, <see cref="MaxMetadataDetachedHardDeadlineInSeconds"/>].
        /// Falls back to <see cref="DefaultMetadataDetachedHardDeadlineInSeconds"/> when the environment
        /// variable is unset.
        /// </summary>
        /// <returns>The hard deadline as a <see cref="TimeSpan"/>.</returns>
        internal static TimeSpan GetMetadataDetachedHardDeadline()
        {
            // Intentionally read (and re-parse) the environment variable on every call rather
            // than caching: the value is overridable at runtime and the read is unit-tested to
            // reflect the current environment. The per-call cost is a single env-var lookup on
            // a metadata-cache-miss path and is negligible relative to the network read it bounds.
            int seconds = ConfigurationManager
                .GetEnvironmentVariable(
                    variable: MetadataDetachedHardDeadlineInSeconds,
                    defaultValue: DefaultMetadataDetachedHardDeadlineInSeconds);
            int clamped = Math.Min(
                Math.Max(seconds, MinMetadataDetachedHardDeadlineInSeconds),
                MaxMetadataDetachedHardDeadlineInSeconds);

            // A user-supplied value outside [Min, Max] is a misconfiguration: clamp it (per the
            // field docs) but emit a one-time warning so the clamp is visible in diagnostics
            // instead of being applied silently. The flag keeps the warning off the hot path
            // after the first occurrence.
            if (clamped != seconds
                && Interlocked.CompareExchange(ref metadataDetachedHardDeadlineClampWarned, 1, 0) == 0)
            {
                DefaultTrace.TraceWarning(
                    "ConfigurationManager: {0}={1}s is outside the supported range [{2}, {3}]s and was clamped to {4}s.",
                    MetadataDetachedHardDeadlineInSeconds,
                    seconds,
                    MinMetadataDetachedHardDeadlineInSeconds,
                    MaxMetadataDetachedHardDeadlineInSeconds,
                    clamped);
            }

            return TimeSpan.FromSeconds(clamped);
        }

        /// <summary>
        /// Guard so the <see cref="GetMetadataDetachedHardDeadline"/> clamp warning is emitted
        /// at most once per process (this method runs on every metadata-cache miss).
        /// </summary>
        private static int metadataDetachedHardDeadlineClampWarned;

        /// <summary>
        /// Gets the AAD scope value to override.
        /// </summary>
        /// <param name="defaultValue">Emoty string for AAD scope if no scope value is provided.</param>
        /// <returns>AAD scope value.</returns>
        public static string AADScopeOverrideValue(
            string defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.AADScopeOverride,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value of the partition level circuit breaker environment variable. Note that, partition level
        /// circuit breaker is disabled by default for both preview and GA releases. The user can set the respective
        /// environment variable 'AZURE_COSMOS_PARTITION_LEVEL_CIRCUIT_BREAKER_ENABLED' to override the value for both preview and GA.
        /// </summary>
        /// <param name="defaultValue">A boolean field containing the default value for partition level circuit breaker.</param>
        /// <returns>A boolean flag indicating if partition level circuit breaker is enabled.</returns>
        public static bool IsPartitionLevelCircuitBreakerEnabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.PartitionLevelCircuitBreakerEnabled,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the interval time in seconds for refreshing stale partition unavailability.
        /// The default value for this interval is 60 seconds. The user can set the respective
        /// environment variable 'AZURE_COSMOS_PPCB_STALE_PARTITION_UNAVAILABILITY_REFRESH_INTERVAL_IN_SECONDS'
        /// to override the value.
        /// </summary>
        /// <param name="defaultValue">An integer containing the default value for the refresh interval in seconds.</param>
        /// <returns>An integer representing the refresh interval in seconds.</returns>
        public static int GetStalePartitionUnavailabilityRefreshIntervalInSeconds(
            int defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.StalePartitionUnavailabilityRefreshIntervalInSeconds,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the allowed partition unavailability duration in seconds.
        /// The default value for this duration is 5 seconds. The user can set the respective
        /// environment variable 'AZURE_COSMOS_PPCB_ALLOWED_PARTITION_UNAVAILABILITY_DURATION_IN_SECONDS'
        /// to override the value.
        /// </summary>
        /// <param name="defaultValue">An integer containing the default unavailability duration in seconds.</param>
        /// <returns>An integer representing the allowed partition unavailability duration in seconds.</returns>
        public static int GetAllowedPartitionUnavailabilityDurationInSeconds(
            int defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.AllowedPartitionUnavailabilityDurationInSeconds,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the consecutive failure count for reads before triggering the per partition circuit breaker flow.
        /// The default value for this interval is 10 consecutive requests within a 1-minute window.
        /// The user can set the respective environment variable 'AZURE_COSMOS_PPCB_CONSECUTIVE_FAILURE_COUNT_FOR_READS' to override the value.
        /// </summary>
        /// <param name="defaultValue">An integer containing the default value for the consecutive failure count.</param>
        /// <returns>An integer representing the consecutive failure count for reads.</returns>
        public static int GetCircuitBreakerConsecutiveFailureCountForReads(
            int defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the consecutive failure count for writes (applicable for multi master accounts) before triggering
        /// the per partition circuit breaker flow. The default value for this interval is 5 consecutive requests within a 1-minute window.
        /// The user can set the respective environment variable 'AZURE_COSMOS_PPCB_CONSECUTIVE_FAILURE_COUNT_FOR_WRITES' to override the value.
        /// </summary>
        /// <param name="defaultValue">An integer containing the default value for the consecutive failure count.</param>
        /// <returns>An integer representing the consecutive failure count for writes.</returns>
        public static int GetCircuitBreakerConsecutiveFailureCountForWrites(
            int defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.CircuitBreakerConsecutiveFailureCountForWrites,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the consecutive faulure count for writes (applicable for multi master accounts) before triggering
        /// the per partition circuit breaker flow. The default value for this interval is 5 consecutive requests within a 1-minute window.
        /// The user can set the respective environment variable 'AZURE_COSMOS_PPCB_TIMEOUT_COUNTER_RESET_WINDOW_IN_MINUTES' to override the value.
        /// </summary>
        /// <param name="defaultValue">An integer containing the default value for the consecutive failure count.</param>
        /// <returns>An double representing the timeout counter reset window in minutes.</returns>
        public static double GetCircuitBreakerTimeoutCounterResetWindowInMinutes(
            double defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.CircuitBreakerTimeoutCounterResetWindowInMinutes,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating whether optimistic direct execution is enabled based on the environment variable override.
        /// </summary>
        public static bool IsOptimisticDirectExecutionEnabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: OptimisticDirectExecutionEnabled,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating whether the hybrid search query plan optimization feature flag should be sent to the gateway
        /// based on the environment variable override.
        /// </summary>
        public static bool IsHybridSearchQueryPlanOptimizationDisabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: HybridSearchQueryPlanOptimizationDisabled,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating if distributed query gateway mode is enabled
        /// based on the environment variable override.
        /// </summary>
        public static bool IsDistributedQueryGatewayModeEnabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: DistributedQueryGatewayModeEnabled,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating if binary encoding is enabled based on the environment variable override.
        /// Note that binary encoding is disabled by default for both preview and GA releases. The user can set the
        /// respective environment variable 'AZURE_COSMOS_BINARY_ENCODING_ENABLED' to override the value for both preview and GA.
        /// This method will eventually be removed once binary encoding is enabled by default for both preview and GA.
        /// </summary>
        /// <returns>A boolean flag indicating if binary encoding is enabled.</returns>
        public static bool IsBinaryEncodingEnabled()
        {
            bool defaultValue = false;
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.BinaryEncodingEnabled,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating if channel multiplexing enabled on TCP channel.
        /// Default: false
        /// </summary>
        /// <returns>A boolean flag indicating if channel multiplexing is enabled.</returns>
        public static bool IsTcpChannelMultiplexingEnabled()
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.TcpChannelMultiplexingEnabled,
                        defaultValue: false);
        }

        /// <summary>
        /// Gets the boolean value indicating if channel multiplexing enabled on TCP channel.
        /// Default: false
        /// </summary>
        /// <returns>A boolean flag indicating if channel multiplexing is enabled.</returns>
        public static bool ForceBypassQueryParsing()
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.BypassQueryParsing,
                        defaultValue: false);
        }

        /// <summary>
        /// Gets the boolean value indicating if length-aware range comparator is enabled.
        /// Default: true for GA and Preview builds, false for INTERNAL builds.
        /// Can be overridden via the AZURE_COSMOS_USE_LENGTH_AWARE_RANGE_COMPARATOR environment variable.
        /// Setting the environment variable to false disables length-aware range comparator across all
        /// usage sites (TryCombine, QueryRangeUtils, PartitionRoutingHelper).
        /// </summary>
        /// <returns>A boolean flag indicating if length-aware range comparator is enabled.</returns>
        public static bool IsLengthAwareRangeComparatorEnabled()
        {
            bool defaultValue = true;
#if INTERNAL
            defaultValue = false;
#endif
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.UseLengthAwareRangeComparator,
                        defaultValue: defaultValue);
        }

        /// <summary>
        /// Gets the boolean value indicating if DNS dot-suffix (FQDN trailing dot) is enabled
        /// for Direct mode TCP connections. When enabled, appends a trailing '.' to hostnames
        /// before DNS resolution, causing the resolver to treat them as absolute (fully qualified)
        /// names and skip search-domain expansion. This avoids unnecessary DNS lookups on Kubernetes
        /// where ndots:5 causes multiple failed search-domain attempts for Cosmos DB endpoints.
        /// Default: false (opt-in).
        /// </summary>
        /// <returns>A boolean flag indicating if TCP DNS dot-suffix is enabled.</returns>
        public static bool IsTcpDnsDotSuffixEnabled()
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.TcpDnsDotSuffixEnabled,
                        defaultValue: false);
        }

        /// <summary>
        /// Gets the boolean value indicating if hub region processing is enabled for read requests
        /// encountering repeated 404/1002 (ReadSessionNotAvailable) errors on single-master accounts.
        /// When enabled, the SDK attaches a hub region header to route read requests to the hub region
        /// for authoritative partition resolution. When disabled, the SDK falls back to the original
        /// retry behavior (route to write region and give up after two 404/1002 attempts).
        /// Default: true.
        /// </summary>
        /// <returns>A boolean flag indicating if hub region processing is enabled.</returns>
        public static bool IsHubRegionProcessingEnabled()
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.HubRegionProcessingEnabled,
                        defaultValue: true);
        }
    }
}
