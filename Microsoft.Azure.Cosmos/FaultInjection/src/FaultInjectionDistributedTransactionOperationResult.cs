//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Describes a single per-operation result to embed in an injected distributed-transaction
    /// coordinator response (the <c>operationResponses[]</c> array of the response body).
    /// Used with <see cref="FaultInjectionServerErrorType.DistributedTransactionCoordinatorError"/> to
    /// reproduce constituent (per-operation) outcomes such as <c>424 FailedDependency</c> or
    /// <c>453 DtcOperationRolledBack</c>.
    /// </summary>
    public sealed class FaultInjectionDistributedTransactionOperationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionDistributedTransactionOperationResult"/> class.
        /// </summary>
        /// <param name="index">The ordinal of the operation within the transaction (matches the request <c>index</c>).</param>
        /// <param name="statusCode">The per-operation HTTP status code.</param>
        /// <param name="subStatusCode">The per-operation sub-status code. Defaults to 0.</param>
        public FaultInjectionDistributedTransactionOperationResult(
            int index,
            int statusCode,
            int subStatusCode = 0)
        {
            this.Index = index;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
        }

        /// <summary>
        /// Gets the ordinal of the operation within the transaction.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the per-operation HTTP status code.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Gets the per-operation sub-status code.
        /// </summary>
        public int SubStatusCode { get; }
    }
}
