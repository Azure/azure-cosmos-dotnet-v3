//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

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
        /// Environment variable name for overriding optimistic direct execution of queries.
        /// </summary>
        internal static readonly string OptimisticDirectExecutionEnabled = "AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED";

        /// <summary>
        /// Environment variable name to disable sending non streaming order by query feature flag to the gateway.
        /// </summary>
        internal static readonly string NonStreamingOrderByQueryFeatureDisabled = "AZURE_COSMOS_NON_STREAMING_ORDER_BY_FLAG_DISABLED";

        /// <summary>
        /// Environment variable name to enable distributed query gateway mode.
        /// </summary>
        internal static readonly string DistributedQueryGatewayModeEnabled = "AZURE_COSMOS_DISTRIBUTED_QUERY_GATEWAY_ENABLED";

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
        /// A read-only string containing the environment variable name for configuring
        /// the first timeout duration before an HTTP call is considered timed out.
        /// Once this timeout is reached, the exponential backoff retry strategy is triggered.
        /// Default value: <c>500ms</c>
        /// </summary>
        internal static readonly string HttpFirstRetryTimeoutValue = "AZURE_COSMOS_SDK_HTTP_FIRST_RETRY_TIMEOUT_VALUE_MS";

        public static T GetEnvironmentVariable<T>(string variable, T defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return (T)Convert.ChangeType(value, typeof(T));
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
        /// Gets the boolean value of the partition level failover environment variable. Note that, partition level failover
        /// is disabled by default for both preview and GA releases. The user can set the  respective environment variable
        /// 'AZURE_COSMOS_PARTITION_LEVEL_FAILOVER_ENABLED' to override the value for both preview and GA. The method will
        /// eventually be removed, once partition level failover is enabled by default for  both preview and GA.
        /// </summary>
        /// <param name="defaultValue">A boolean field containing the default value for partition level failover.</param>
        /// <returns>A boolean flag indicating if partition level failover is enabled.</returns>
        public static bool IsPartitionLevelFailoverEnabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.PartitionLevelFailoverEnabled,
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
        /// Gets the boolean value indicating whether the non streaming order by query feature flag should be sent to the gateway
        /// based on the environment variable override.
        /// </summary>
        public static bool IsNonStreamingOrderByQueryFeatureDisabled(
            bool defaultValue)
        {
            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: NonStreamingOrderByQueryFeatureDisabled,
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
    }
}
