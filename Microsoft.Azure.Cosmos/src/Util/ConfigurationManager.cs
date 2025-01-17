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


    }
}
