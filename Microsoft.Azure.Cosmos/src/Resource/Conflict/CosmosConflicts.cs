//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Operations for reading/querying conflicts in a Azure Cosmos container.
    /// </summary>
    public abstract class CosmosConflicts
    {
        /// <summary>
        /// Returns a reference to a conflict object. 
        /// </summary>
        /// <param name="id">The cosmos conflict id.</param>
        public abstract CosmosConflict this[string id] { get; }

        /// <summary>
        /// Obtains an iterator to go through the <see cref="CosmosConflict"/> on an Azure Cosmos container.
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <returns></returns>
        public abstract CosmosResultSetIterator<CosmosConflictSettings> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null);
    }
}
