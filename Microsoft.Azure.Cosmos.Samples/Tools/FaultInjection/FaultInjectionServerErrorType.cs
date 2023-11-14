//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Types of ServerErrors that can be injected
    /// </summary>
    public enum FaultInjectionServerErrorType
    {
        /// <summary>
        /// 410: Gone from server
        /// </summary>
        Gone,

        /// <summary>
        /// 449: RetryWith from server
        /// </summary>
        RetryWith,

        /// <summary>
        /// 500: Internal Server Error from server
        /// </summary>
        InternalServerEror,

        /// <summary>
        /// 429:Too Many Requests from server
        /// </summary>
        TooManyRequests,

        /// <summary>
        /// 404-1002: Read session not available from server
        /// </summary>
        ReadSessionNotAvailable,

        /// <summary>
        /// 408: Request Timeout from server
        /// </summary>
        Timeout,

        /// <summary>
        /// 410-1008: Partition is splitting
        /// </summary>
        PartitionIsSplitting,

        /// <summary>
        /// 410-1008: Partition is migrating from server
        /// </summary>
        PartitionIsMigrating,

        /// <summary>
        /// Used to simulate a transient timeout/broken connection when over request timeout
        /// </summary>
        ResponseDelay,

        /// <summary>
        ///  Used to simulate hight channel acquisiton. 
        ///  When over a connection timeouts can simulate connectionTimeoutException
        /// </summary>
        ConnectionDelay
    }
}
