//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    //Should match string in SchemaConstants.h :: ConsistencyLevel
    /// <summary> 
    /// These are the consistency levels supported by the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// The requested Consistency Level must match or be weaker than that provisioned for the database account.
    /// </remarks>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/consistency-levels"/>
    public enum ConsistencyLevel
    {
        /// <summary>
        /// Strong Consistency guarantees that read operations always return the value that was last written.
        /// </summary>
        Strong,

        /// <summary>
        /// Bounded Staleness guarantees that reads are not too out-of-date. This can be configured based on number of operations (MaxStalenessPrefix) 
        /// or time (MaxStalenessIntervalInSeconds).  For more information on MaxStalenessPrefix and MaxStalenessIntervalInSeconds, please see <see cref="AccountConsistency"/>.
        /// </summary>
        BoundedStaleness,

        /// <summary>
        /// Session Consistency guarantees monotonic reads (you never read old data, then new, then old again), monotonic writes (writes are ordered) 
        /// and read your writes (your writes are immediately visible to your reads) within any single session. 
        /// </summary>
        Session,

        /// <summary>
        /// Eventual Consistency guarantees that reads will return a subset of writes. All writes 
        /// will be eventually be available for reads.
        /// </summary>
        Eventual,

        /// <summary>
        /// ConsistentPrefix Consistency guarantees that reads will return some prefix of all writes with no gaps.
        /// All writes will be eventually be available for reads.
        /// </summary>
        ConsistentPrefix
    }
}
