//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes a synthetic distributed-transaction coordinator response to inject when a fault
    /// injection rule uses <see cref="FaultInjectionServerErrorType.DistributedTransactionCoordinatorError"/>.
    /// This lets a test reproduce any documented DTC envelope outcome — status code, sub-status,
    /// the body <c>isRetriable</c> flag, an optional retry-after hint, and per-operation results —
    /// without a real coordinator backend.
    /// </summary>
    /// <remarks>
    /// The envelope status/sub-status are emitted on the HTTP response (status line and
    /// <c>x-ms-substatus</c>); <c>isRetriable</c> and <c>operationResponses[]</c> are emitted in the
    /// JSON body, matching the DTC REST contract the SDK parses.
    /// </remarks>
    public sealed class FaultInjectionDistributedTransactionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionDistributedTransactionResponse"/> class.
        /// </summary>
        /// <param name="statusCode">The envelope (transaction-level) HTTP status code.</param>
        /// <param name="subStatusCode">The envelope sub-status code. Emitted only when non-zero. Defaults to 0.</param>
        /// <param name="isRetriable">The body <c>isRetriable</c> flag that drives the committer's outer retry loop. Defaults to false.</param>
        /// <param name="retryAfter">An optional <c>x-ms-retry-after-ms</c> backoff hint. Defaults to none.</param>
        /// <param name="operationResults">Optional per-operation results to embed in <c>operationResponses[]</c>. Defaults to none.</param>
        /// <param name="emptyBody">
        /// When true, no response body is emitted at all (the HTTP response carries <c>Content == null</c>),
        /// reproducing a bodyless coordinator envelope such as 408, 449, 429, or 500 infra failures. With an
        /// empty body the SDK's inner <c>ClientRetryPolicy</c> owns retry classification (the outer committer
        /// loop only retries on a body-borne <c>isRetriable: true</c>). When true, <paramref name="isRetriable"/>
        /// and <paramref name="operationResults"/> are ignored because there is no body to carry them. Defaults to false.
        /// </param>
        public FaultInjectionDistributedTransactionResponse(
            int statusCode,
            int subStatusCode = 0,
            bool isRetriable = false,
            TimeSpan? retryAfter = null,
            IReadOnlyList<FaultInjectionDistributedTransactionOperationResult>? operationResults = null,
            bool emptyBody = false)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.IsRetriable = isRetriable;
            this.RetryAfter = retryAfter;
            this.OperationResults = operationResults ?? Array.Empty<FaultInjectionDistributedTransactionOperationResult>();
            this.EmptyBody = emptyBody;
        }

        /// <summary>
        /// Gets the envelope (transaction-level) HTTP status code.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Gets the envelope sub-status code. Emitted on the wire only when non-zero.
        /// </summary>
        public int SubStatusCode { get; }

        /// <summary>
        /// Gets a value indicating whether the response body sets <c>isRetriable: true</c>.
        /// </summary>
        public bool IsRetriable { get; }

        /// <summary>
        /// Gets the optional <c>x-ms-retry-after-ms</c> backoff hint.
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        /// <summary>
        /// Gets the per-operation results embedded in the <c>operationResponses[]</c> array. May be empty.
        /// </summary>
        public IReadOnlyList<FaultInjectionDistributedTransactionOperationResult> OperationResults { get; }

        /// <summary>
        /// Gets a value indicating whether the injected response omits its body entirely (<c>Content == null</c>),
        /// reproducing a bodyless coordinator envelope so the SDK's inner <c>ClientRetryPolicy</c> drives retry.
        /// </summary>
        public bool EmptyBody { get; }
    }
}
