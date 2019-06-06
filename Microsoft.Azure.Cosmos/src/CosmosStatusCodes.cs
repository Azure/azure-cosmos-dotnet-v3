//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Represents the cosmos status codes
    /// </summary>
    /// <remarks>
    /// Format is (HttpStatus)(substatus) (xxx)(xxxx)
    /// HttpStatusCode.NotFound(404) SubStatusCode.Unknown(0) == 4040000
    /// HttpStatusCode.NotFound(404) SubStatusCode.NameCacheIsStale(1001) == 4041001
    /// </remarks>
    public enum CosmosStatusCodes
    {
        /// <summary>
        /// Current Resource is not found
        /// </summary>
        NotFound = 4040000,

        /// <summary>
        /// Name cache is stale
        /// </summary>
        NotFoundWithNameCacheIsStale = 4041000,

        /// <summary>
        /// Partition Key range is gone
        /// </summary>
        NotFoundWithPartitionKeyRangeGone = 4041002,

        /// <summary>
        /// Parent Resource is deleted
        /// </summary>
        NotFoundWithParentResource = 4049000,

        /// <summary>
        /// Client hit an unknown error
        /// </summary>
        ClientError = 900,

        /// <summary>
        /// Client side hit a socket error
        /// </summary>
        ClientErrorWithSocketError = 9001001
    }
}
