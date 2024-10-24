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
        /// A read-only string containing the environment variable name for enabling binary encoding. This will eventually
        /// be removed once binary encoding is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string BinaryEncodingEnabled = "AZURE_COSMOS_BINARY_ENCODING_ENABLED";

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
    }
}
