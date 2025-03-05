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
        /// 410: Gone from server. Only Applicable for Direct mode calls.
        /// </summary>
        Gone,

        /// <summary>
        /// 449: RetryWith from server
        /// </summary>
        RetryWith,

        /// <summary>
        /// 500: Internal Server Error from server
        /// </summary>
        InternalServerError,

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
        /// In this case, the request will be sent to the server before the delay
        /// </summary>
        ResponseDelay,

        /// <summary>
        /// Used to simulate a transient timeout/broken connection when over request timeout
        /// In this case, the delay will occur before the request is sent to the server
        /// </summary>
        SendDelay,

        /// <summary>
        ///  Used to simulate hight channel acquisiton. 
        ///  When over a connection timeouts can simulate connectionTimeoutException
        /// </summary>
        ConnectionDelay,

        /// <summary>
        /// 503: Service Unavailable from server
        /// </summary>
        ServiceUnavailable,

        /// <summary>
        /// 404:1008 Database account not found from gateway
        /// </summary>
        DatabaseAccountNotFound,
    }
}
