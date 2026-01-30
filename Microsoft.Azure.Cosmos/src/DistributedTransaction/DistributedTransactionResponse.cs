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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
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
        private const string IdempotencyTokenHeader = "x-ms-dtc-operation-id";

        private List<DistributedTransactionOperationResult> results;
        private bool isDisposed;

        private DistributedTransactionResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            Headers headers,
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializer,
            ITrace trace,
            Guid idempotencyToken,
            string serverDiagnostics = null)
        {
            this.Headers = headers;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.ErrorMessage = errorMessage;
            this.Operations = operations;
            this.SerializerCore = serializer;
            this.Trace = trace;
            this.IdempotencyToken = idempotencyToken;
            this.ServerDiagnostics = serverDiagnostics;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionResponse"/> class.
        /// </summary>
        protected DistributedTransactionResponse()
        {
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
                this.ThrowIfDisposed();

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
        public virtual Headers Headers { get; }

        /// <summary>
        /// Gets the ActivityId that identifies the server request made to execute the transaction.
        /// </summary>
        public virtual string ActivityId => this.Headers?.ActivityId;

        /// <summary>
        /// Gets the request charge for the distributed transaction request.
        /// </summary>
        public virtual double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <summary>
        /// Gets the HTTP status code for the distributed transaction response.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction was processed successfully.
        /// </summary>
        public virtual bool IsSuccessStatusCode => (int)this.StatusCode >= 200 && (int)this.StatusCode <= 299;

        /// <summary>
        /// Gets the error message associated with the distributed transaction response, if any.
        /// </summary>
        public virtual string ErrorMessage { get; }

        /// <summary>
        /// Gets the number of operation results in the distributed transaction response.
        /// </summary>
        public virtual int Count => this.results?.Count ?? 0;

        /// <summary>
        /// Gets the idempotency token associated with this distributed transaction.
        /// </summary>
        public virtual Guid IdempotencyToken { get; }

        /// <summary>
        /// Gets the server-side diagnostic information for the transaction.
        /// </summary>
        public virtual string ServerDiagnostics { get; }

        internal virtual SubStatusCodes SubStatusCode { get; }

        internal virtual CosmosSerializerCore SerializerCore { get; }

        internal IReadOnlyList<DistributedTransactionOperation> Operations { get; }

        internal ITrace Trace { get; }

        /// <summary>
        /// Returns an enumerator that iterates through the operation results.
        /// </summary>
        /// <returns>An enumerator for the operation results.</returns>
        public virtual IEnumerator<DistributedTransactionOperationResult> GetEnumerator()
        {
            return this.results?.GetEnumerator()
                ?? ((IList<DistributedTransactionOperationResult>)Array.Empty<DistributedTransactionOperationResult>()).GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the operation results.
        /// </summary>
        /// <returns>An enumerator for the operation results.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="DistributedTransactionResponse"/> and optionally releases the managed resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a <see cref="DistributedTransactionResponse"/> from a <see cref="ResponseMessage"/>.
        /// </summary>
        internal static async Task<DistributedTransactionResponse> FromResponseMessageAsync(
            ResponseMessage responseMessage,
            DistributedTransactionServerRequest serverRequest,
            CosmosSerializerCore serializer,
            Guid requestIdempotencyToken,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace createResponseTrace = trace.StartChild("Create Distributed Transaction Response", TraceComponent.Batch, TraceLevel.Info))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract idempotency token from response headers, fallback to request token if not present
                Guid idempotencyToken = GetIdempotencyTokenFromHeaders(responseMessage.Headers, requestIdempotencyToken);

                DistributedTransactionResponse response = null;
                MemoryStream memoryStream = null;

                try
                {
                    if (responseMessage.Content != null)
                    {
                        Stream content = responseMessage.Content;

                        // Ensure the stream is seekable
                        if (!content.CanSeek)
                        {
                            memoryStream = new MemoryStream();
                            await responseMessage.Content.CopyToAsync(memoryStream);
                            memoryStream.Position = 0;
                            content = memoryStream;
                        }

                        response = await PopulateFromJsonContentAsync(
                            content,
                            responseMessage,
                            serverRequest,
                            serializer,
                            idempotencyToken,
                            createResponseTrace,
                            cancellationToken);
                    }

                    // If we couldn't parse JSON content or there was no content, create default response
                    response ??= new DistributedTransactionResponse(
                        responseMessage.StatusCode,
                        responseMessage.Headers.SubStatusCode,
                        responseMessage.ErrorMessage,
                        responseMessage.Headers,
                        serverRequest.Operations,
                        serializer,
                        createResponseTrace,
                        idempotencyToken);

                    // Validate results count matches operations count
                    if (response.results == null || response.results.Count != serverRequest.Operations.Count)
                    {
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            // Server should guarantee results count equals operations count on success
                            return new DistributedTransactionResponse(
                                HttpStatusCode.InternalServerError,
                                SubStatusCodes.Unknown,
                                ClientResources.InvalidServerResponse,
                                responseMessage.Headers,
                                serverRequest.Operations,
                                serializer,
                                createResponseTrace,
                                idempotencyToken);
                        }

                        response.CreateAndPopulateResults(serverRequest.Operations, createResponseTrace);
                    }

                    return response;
                }
                finally
                {
                    memoryStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Disposes the disposable members held by this class.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
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
            }

            this.results = null;
            this.isDisposed = true;
        }

        private static Guid GetIdempotencyTokenFromHeaders(Headers headers, Guid fallbackToken)
        {
            if (headers != null &&
                headers.TryGetValue(IdempotencyTokenHeader, out string tokenValue) &&
                Guid.TryParse(tokenValue, out Guid idempotencyToken))
            {
                return idempotencyToken;
            }

            return fallbackToken;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(DistributedTransactionResponse));
            }
        }

        private static async Task<DistributedTransactionResponse> PopulateFromJsonContentAsync(
            Stream content,
            ResponseMessage responseMessage,
            DistributedTransactionServerRequest serverRequest,
            CosmosSerializerCore serializer,
            Guid idempotencyToken,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            List<DistributedTransactionOperationResult> results = new List<DistributedTransactionOperationResult>();

            try
            {
                using (JsonDocument responseJson = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken))
                {
                    JsonElement root = responseJson.RootElement;

                    // Parse operation results from "operationResponses" array
                    if (root.TryGetProperty("operationResponses", out JsonElement operationResponses) &&
                        operationResponses.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement operationElement in operationResponses.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            DistributedTransactionOperationResult operationResult = DistributedTransactionOperationResult.FromJson(operationElement);
                            operationResult.Trace = trace;
                            operationResult.SessionToken ??= responseMessage.Headers.Session;
                            operationResult.ActivityId = responseMessage.Headers.ActivityId;
                            results.Add(operationResult);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, return null to fall back to default response
                return null;
            }

            HttpStatusCode finalStatusCode = responseMessage.StatusCode;
            SubStatusCodes finalSubStatusCode = responseMessage.Headers.SubStatusCode;

            // Promote operation error status for MultiStatus responses
            if ((int)finalStatusCode == (int)StatusCodes.MultiStatus)
            {
                foreach (DistributedTransactionOperationResult result in results)
                {
                    if ((int)result.StatusCode != (int)StatusCodes.FailedDependency &&
                        (int)result.StatusCode >= (int)StatusCodes.StartingErrorCode)
                    {
                        finalStatusCode = result.StatusCode;
                        finalSubStatusCode = result.SubStatusCode;
                        break;
                    }
                }
            }

            return new DistributedTransactionResponse(
                finalStatusCode,
                finalSubStatusCode,
                responseMessage.ErrorMessage,
                responseMessage.Headers,
                serverRequest.Operations,
                serializer,
                trace,
                idempotencyToken)
            {
                results = results
            };
        }

        private void CreateAndPopulateResults(
            IReadOnlyList<DistributedTransactionOperation> operations,
            ITrace trace)
        {
            this.results = new List<DistributedTransactionOperationResult>(operations.Count);

            for (int i = 0; i < operations.Count; i++)
            {
                this.results.Add(new DistributedTransactionOperationResult(this.StatusCode)
                {
                    SubStatusCode = this.SubStatusCode,
                    SessionToken = this.Headers?.Session,
                    ActivityId = this.ActivityId,
                    Trace = trace
                });
            }
        }
    }
}
