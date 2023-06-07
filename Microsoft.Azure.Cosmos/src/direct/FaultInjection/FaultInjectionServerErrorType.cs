//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    /// <summary>
    /// Types of ServerErrors that can be injected
    /// </summary>
    public enum FaultInjectionServerErrorType
    {
        /// <summary>
        /// 410: Gone from server
        /// </summary>
        GONE,

        /// <summary>
        /// 449: RetryWith from server
        /// </summary>
        RETRY_WITH,

        /// <summary>
        /// 500: Internal Server Error from server
        /// </summary>
        INTERNAL_SERVER_ERROR,

        /// <summary>
        /// 429:Too Many Requests from server
        /// </summary>
        TOO_MANY_REQUESTS,

        /// <summary>
        /// 404-1002: Read session not available from server
        /// </summary>
        READ_SESSION_NOT_AVAILABLE,

        /// <summary>
        /// 408: Request Timeout from server
        /// </summary>
        TIMEOUT,

        /// <summary>
        /// 410-1008: Partition is migrating from server
        /// </summary>
        PARTITION_IS_MIGRATING,

        /// <summary>
        /// Used to simulate a transient timeout/broken connection when over request timeout
        /// </summary>
        RESPONSE_DELAY, 

        /// <summary>
        ///  Used to simulate hight channel acquisiton. 
        ///  When over a connection timeoutm can simulate connectionTimeoutException
        /// </summary>
        CONNECTION_DELAY
    }
}
