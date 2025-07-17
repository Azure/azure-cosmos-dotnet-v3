//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Configuration settings for batch operations.
    /// </summary>
    internal static class BatchConfiguration
    {
        /// <summary>
        /// Gets the maximum number of operations allowed in a direct mode batch request.
        /// This value can be customized using the COSMOS_MAX_OPERATIONS_IN_DIRECT_MODE_BATCH_REQUEST environment variable.
        /// If the environment variable is not set, the default value from Constants.MaxOperationsInDirectModeBatchRequest is used.
        /// </summary>
        /// <returns>The maximum number of operations allowed in a direct mode batch request.</returns>
        public static int GetMaxOperationsInDirectModeBatchRequest()
        {
            return ConfigurationManager.GetMaxOperationsInDirectModeBatchRequest();
        }
    }
}