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
        /// This will eventually be removed oncereplica valdiatin is enabled by default for both preview
        /// and GA.
        /// </summary>
        private static readonly string ReplicaConnectivityValidationEnabled = "AZURE_COSMOS_REPLICA_VALIDATION_ENABLED";

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
        /// <returns>A boolean flag indicating if replica validation is enabled.</returns>
        public static bool IsReplicaAddressValidationEnabled()
        {
            bool replicaValidationDefaultValue = false;
#if PREVIEW
            replicaValidationDefaultValue = true;
#endif

            return ConfigurationManager
                    .GetEnvironmentVariable(
                        variable: ConfigurationManager.ReplicaConnectivityValidationEnabled,
                        defaultValue: replicaValidationDefaultValue);
        }
    }
}
