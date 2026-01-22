// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the response for a distributed transaction operation.
    /// </summary>
#if INTERNAL
        public 
#else
    internal
#endif
    class DistributedTransactionResponse : IReadOnlyList<DistributedTransactionOperationResult>, IDisposable
    {
        private List<DistributedTransactionOperationResult> results;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionResponse"/> class.
        /// This method is intended to be used only when a response from the server is not available.
        /// </summary>
        /// <param name="statusCode">Indicates why the transaction was not processed.</param>
        /// <param name="subStatusCode">Provides further details about why the transaction was not processed.</param>
        /// <param name="errorMessage">The reason for failure.</param>
        /// <param name="operations">Operations that were to be executed.</param>
        /// <param name="trace">Diagnostics for the operation.</param>
        internal DistributedTransactionResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            IReadOnlyList<DistributedTransactionOperation> operations,
            ITrace trace)
            : this(statusCode,
                  subStatusCode,
                  errorMessage,
                  new Headers(),
                  trace: trace,
                  operations: operations,
                  serializer: null)
        {
            this.CreateAndPopulateResults(operations, trace);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionResponse"/> class.
        /// </summary>
        protected DistributedTransactionResponse()
        {
        }

        private DistributedTransactionResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            Headers headers,
            ITrace trace,
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializer)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.ErrorMessage = errorMessage;
            this.Operations = operations;
            this.SerializerCore = serializer;
            this.Headers = headers;
            this.Diagnostics = new CosmosTraceDiagnostics(trace ?? NoOpTrace.Singleton);
        }

        /// <summary>
        /// Gets the <see cref="DistributedTransactionOperationResult"/> at the specified index in the response.
        /// </summary>
        /// <param name="index">The zero-based index of the operation result to get.</param>
        /// <returns>The <see cref="DistributedTransactionOperationResult"/> at the specified index.</returns>
        public virtual DistributedTransactionOperationResult this[int index]
        {
            get
            {
                if (this.results == null || index < 0 || index >= this.results.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
                }

                return this.results[index];
            }
        }

        /// <summary>
        /// Gets the headers associated with the distributed transaction response.
        /// </summary>
        public virtual Headers Headers { get; internal set; }

        /// <summary>
        /// Gets the request charge for the transaction request.
        /// </summary>
        public virtual double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <summary>
        /// Gets the amount of time to wait before retrying this or any other request within Cosmos container or collection due to throttling.
        /// </summary>
        public virtual TimeSpan? RetryAfter => this.Headers?.RetryAfter;

        /// <summary>
        /// Gets the HTTP status code for the distributed transaction response.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the transaction was processed successfully.
        /// </summary>
        public virtual bool IsSuccessStatusCode => ((int)this.StatusCode >= 200) && ((int)this.StatusCode <= 299);

        /// <summary>
        /// Gets a value indicating whether the failed operation can be retried.
        /// </summary>
        public virtual bool IsRetriable { get; internal set; }

        internal virtual SubStatusCodes SubStatusCode { get; }

        internal IReadOnlyList<DistributedTransactionOperation> Operations { get; set; }

        internal virtual CosmosSerializerCore SerializerCore { get; }

        /// <summary>
        /// Gets the error message associated with the distributed transaction response, if any.
        /// </summary>
        public virtual string ErrorMessage { get; internal set; }

        /// <summary>
        /// Gets the number of operation results in the distributed transaction response.
        /// </summary>
        public virtual int Count => this.results?.Count ?? 0;

        /// <summary>
        /// Gets the cosmos diagnostic information for the current request to Azure Cosmos DB service.
        /// </summary>
        public virtual CosmosDiagnostics Diagnostics { get; }

        /// <summary>
        /// Gets the result of the operation at the provided index in the transaction - the returned result has a Resource of provided type.
        /// </summary>
        /// <typeparam name="T">Type to which the Resource in the operation result needs to be deserialized to, when present.</typeparam>
        /// <param name="index">0-based index of the operation in the transaction whose result needs to be returned.</param>
        /// <returns>Result of transaction operation that contains a Resource deserialized to specified type.</returns>
        public virtual DistributedTransactionOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            DistributedTransactionOperationResult result = this.results[index];

            T resource = default;
            if (result.ResourceStream != null && this.SerializerCore != null)
            {
                resource = this.SerializerCore.FromStream<T>(result.ResourceStream);
            }

            return new DistributedTransactionOperationResult<T>(result, resource);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of distributed transaction operation results.
        /// </summary>
        /// <returns>An enumerator for the collection of <see cref="DistributedTransactionOperationResult"/> objects.</returns>
        public virtual IEnumerator<DistributedTransactionOperationResult> GetEnumerator()
        {
            return this.results?.GetEnumerator() ?? ((IEnumerable<DistributedTransactionOperationResult>)Array.Empty<DistributedTransactionOperationResult>()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Disposes the current <see cref="DistributedTransactionResponse"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Disposes the disposable members held by this class.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose managed resources or not.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing && this.results != null)
            {
                foreach (DistributedTransactionOperationResult result in this.results)
                {
                    result.ResourceStream?.Dispose();
                }

                this.results = null;
            }

            this.isDisposed = true;
        }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionResponse"/> from a <see cref="ResponseMessage"/>.
        /// </summary>
        /// <param name="responseMessage">The response message from the API.</param>
        /// <param name="operations">The operations that were executed.</param>
        /// <param name="serializer">The serializer to use for deserializing operation results.</param>
        /// <param name="trace">The trace for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="DistributedTransactionResponse"/>.</returns>
#pragma warning disable IDE0060 // Remove unused parameter - cancellationToken reserved for future async operations
        internal static async Task<DistributedTransactionResponse> FromResponseMessageAsync(
            ResponseMessage responseMessage,
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializer,
            ITrace trace,
            CancellationToken cancellationToken)
#pragma warning restore IDE0060
        {
            using (ITrace createResponseTrace = trace.StartChild("Create Distributed Transaction Response", TraceComponent.Batch, TraceLevel.Info))
            {
                using (responseMessage)
                {
                    DistributedTransactionResponse response = null;

                    if (responseMessage.Content != null)
                    {
                        Stream content = responseMessage.Content;

                        // Ensure stream is seekable
                        if (!responseMessage.Content.CanSeek)
                        {
                            content = new MemoryStream();
                            await responseMessage.Content.CopyToAsync(content).ConfigureAwait(false);
                            content.Position = 0;
                        }

                        response = PopulateFromContent(
                            content,
                            responseMessage,
                            operations,
                            serializer,
                            createResponseTrace);
                    }

                    response ??= new DistributedTransactionResponse(
                        responseMessage.StatusCode,
                        responseMessage.Headers.SubStatusCode,
                        responseMessage.ErrorMessage,
                        responseMessage.Headers,
                        createResponseTrace,
                        operations,
                        serializer);

                    if (response.results == null || response.results.Count != operations.Count)
                    {
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            response = new DistributedTransactionResponse(
                                HttpStatusCode.InternalServerError,
                                SubStatusCodes.Unknown,
                                ClientResources.InvalidServerResponse,
                                responseMessage.Headers,
                                createResponseTrace,
                                operations,
                                serializer);
                        }

                        return response;
                    }
                    return response;
                }
            }
        }

        /// <summary>
        /// Populates the response from the content stream by deserializing operation results.
        /// Maps the API response (CoordinatorToSDKDTCResponse) to SDK response.
        /// </summary>
#pragma warning disable IDE0060
        private static DistributedTransactionResponse PopulateFromContent(
            Stream content,
            ResponseMessage responseMessage,
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializer,
            ITrace trace)
#pragma warning restore IDE0060
        {
            // TODO: Implement deserialization of the API response format when finalized.
            return null;
        }

        private void CreateAndPopulateResults(
            IReadOnlyList<DistributedTransactionOperation> operations, 
            ITrace trace, 
            int retryAfterMilliseconds = 0)
        {
            this.results = new List<DistributedTransactionOperationResult>();

            for (int i = 0; i < operations.Count; i++)
            {
                DistributedTransactionOperationResult result = new DistributedTransactionOperationResult(this.StatusCode)
                {
                    SubStatusCode = this.SubStatusCode,
                    RetryAfter = TimeSpan.FromMilliseconds(retryAfterMilliseconds),
                    SessionToken = this.Headers?.Session,
                    Trace = trace
                };

                this.results.Add(result);
            }
        }
    }
}
