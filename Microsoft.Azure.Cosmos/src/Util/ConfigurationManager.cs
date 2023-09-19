//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal static class ConfigurationManager
    {
        /// <summary>
        /// A read-only string containing the environment variablename for enabling replica validation.
        /// This will eventually be removed once replica valdiatin is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string ReplicaConnectivityValidationEnabled = "AZURE_COSMOS_REPLICA_VALIDATION_ENABLED";

        /// <summary>
        /// A read-only string containing the environment variablename for enabling per partition automatic failover.
        /// This will eventually be removed once per partition automatic failover is enabled by default for both preview
        /// and GA.
        /// </summary>
        internal static readonly string PartitionLevelFailoverEnabled = "AZURE_COSMOS_PARTITION_LEVEL_FAILOVER_ENABLED";

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
            bool replicaValidationDefaultValue = false;

            if (connectionPolicy != null
                && connectionPolicy.EnableAdvancedReplicaSelectionForTcp.HasValue)
            {
                return connectionPolicy.EnableAdvancedReplicaSelectionForTcp.Value;
            }

            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.ReplicaConnectivityValidationEnabled,
                        defaultValue: replicaValidationDefaultValue);
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
    }
}
