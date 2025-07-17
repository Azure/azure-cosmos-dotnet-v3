//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Configuration settings for batch operations.
    /// </summary>
    internal static class BatchConfiguration
    {
        private const string MaxOperationsEnvironmentVariableName = "COSMOS_MAX_OPERATIONS_IN_DIRECT_MODE_BATCH_REQUEST";

        /// <summary>
        /// Gets the maximum number of operations allowed in a direct mode batch request.
        /// This value can be customized using the COSMOS_MAX_OPERATIONS_IN_DIRECT_MODE_BATCH_REQUEST environment variable.
        /// If the environment variable is not set, the default value from Constants.MaxOperationsInDirectModeBatchRequest is used.
        /// </summary>
        /// <returns>The maximum number of operations allowed in a direct mode batch request.</returns>
        public static int GetMaxOperationsInDirectModeBatchRequest()
        {
            string environmentValue = Environment.GetEnvironmentVariable(MaxOperationsEnvironmentVariableName);
            
            if (string.IsNullOrEmpty(environmentValue))
            {
                return Constants.MaxOperationsInDirectModeBatchRequest;
            }

            if (int.TryParse(environmentValue, out int parsedValue))
            {
                if (parsedValue <= 0)
                {
                    throw new ArgumentException(
                        $"Environment variable {MaxOperationsEnvironmentVariableName} must be a positive integer. Current value: {environmentValue}");
                }

                return parsedValue;
            }

            throw new ArgumentException(
                $"Environment variable {MaxOperationsEnvironmentVariableName} must be a valid integer. Current value: {environmentValue}");
        }
    }
}