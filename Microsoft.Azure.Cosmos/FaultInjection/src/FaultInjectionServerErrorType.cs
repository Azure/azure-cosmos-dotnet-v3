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
        /// 403:1008 Database account not found from gateway
        /// </summary>
        DatabaseAccountNotFound,

        /// <summary>
        /// 410:1022 Lease not Found
        /// </summary>
        LeaseNotFound,

        /// <summary>
        /// 401: Unauthorized
        /// </summary>
        Unauthorized,

        /// <summary>
        /// 401:5013 AAD token revoked
        /// </summary>
        AadTokenRevoked,

        /// <summary>
        /// Injects a synthesized distributed-transaction coordinator error response. The injected envelope
        /// can be any documented coordinator outcome — retriable or terminal — described by
        /// <see cref="FaultInjectionServerErrorResultBuilder.WithDistributedTransactionResponse"/>
        /// (status / sub-status, per-operation results, and the body <c>isRetriable</c> flag).
        /// Only supported for <see cref="FaultInjectionOperationType.DistributedReadTransaction"/>
        /// and <see cref="FaultInjectionOperationType.DistributedWriteTransaction"/> operation types.
        /// </summary>
        DistributedTransactionCoordinatorError
    }
}
