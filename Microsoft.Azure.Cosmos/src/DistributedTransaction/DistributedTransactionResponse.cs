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
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents the response for a distributed transaction operation.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class DistributedTransactionResponse : IReadOnlyList<DistributedTransactionOperationResult>, IDisposable
    {
        private List<DistributedTransactionOperationResult> results;
        private bool isDisposed;

        private DistributedTransactionResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            Headers headers,
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializer,
            Guid idempotencyToken,
            bool isRetriable = false)
        {
            this.Headers = headers;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.ErrorMessage = errorMessage;
            this.Operations = operations;
            this.SerializerCore = serializer;
            this.IdempotencyToken = idempotencyToken;
            this.IsRetriable = isRetriable;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionResponse"/> class.
        /// </summary>
        protected DistributedTransactionResponse()
        {
        }

        /// <summary>
        /// Gets the <see cref="DistributedTransactionOperationResult"/> for the request operation at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the request operation whose result to get.</param>
        /// <returns>The <see cref="DistributedTransactionOperationResult"/> for the request operation at the specified index.</returns>
        /// <remarks>
        /// Results may arrive out of request order; the SDK reorders them by per-operation <c>index</c>,
        /// so on a successful response <c>response[i]</c> is the i-th submitted operation. Payloads that
        /// can't be mapped back (missing, duplicate, or out-of-range indices) fail closed (HTTP 500).
        /// </remarks>
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
        /// Gets the result of the operation at the provided index in the transaction — the returned result
        /// has a <see cref="DistributedTransactionOperationResult{T}.Resource"/> of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to which the resource body should be deserialized.</typeparam>
        /// <param name="index">Zero-based index of the operation in the transaction whose result is returned.</param>
        /// <returns>
        /// A <see cref="DistributedTransactionOperationResult{T}"/> whose <c>Resource</c> is the deserialized item,
        /// or <c>default(<typeparamref name="T"/>)</c> when the server did not return a body for that operation.
        /// </returns>
        /// <remarks>
        /// <c>index</c> refers to the i-th operation submitted, regardless of the order the coordinator
        /// returned results (see the indexer for reordering and fail-closed details).
        /// The underlying <see cref="DistributedTransactionOperationResult.ResourceStream"/> is left intact and
        /// remains readable after this call, so this method may be invoked multiple times (with the same or
        /// different <typeparamref name="T"/>) for the same index, and direct access via the indexer continues
        /// to work afterwards.
        /// </remarks>
        public virtual DistributedTransactionOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            this.ThrowIfDisposed();

            DistributedTransactionOperationResult result = this[index];

            T resource = default;
            if (result.ResourceStream != null)
            {
                if (this.SerializerCore == null)
                {
                    throw new InvalidOperationException(
                        "A serializer is required to deserialize the operation resource but none was set on this response.");
                }

                // CosmosSerializer.FromStream<T> takes ownership of (and disposes) its input stream.
                // Create an independent snapshot so this method is safe to call multiple times and
                // the caller can still access ResourceStream directly.
                using Stream snapshot = DistributedTransactionOperationResult.CreateSnapshot(result.ResourceStream);
                resource = this.SerializerCore.FromStream<T>(snapshot);
            }

            return new DistributedTransactionOperationResult<T>(result, resource);
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
        /// Gets the error message associated with the distributed transaction response.
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
        /// Gets a value indicating whether the transaction is safe to retry with the same idempotency token.
        /// </summary>
        public virtual bool IsRetriable { get; }

        /// <summary>
        /// Gets the diagnostic string from the coordinator describing the transaction outcome
        /// </summary>
        public virtual string DiagnosticString { get; private set; }

        /// <summary>
        /// Gets the client-side diagnostics for the distributed transaction, covering the full
        /// retry loop and per-attempt spans (address resolution, network, retries, latency).
        /// </summary>
        /// <remarks>Non-null when the response is returned from <c>CommitTransactionAsync</c>. The SDK sets this property
        /// before returning; callers should treat it as non-null in normal usage but guard defensively in edge cases.</remarks>
        public virtual CosmosDiagnostics Diagnostics { get; internal set; }

        internal virtual SubStatusCodes SubStatusCode { get; }

        internal virtual CosmosSerializerCore SerializerCore { get; }

        internal IReadOnlyList<DistributedTransactionOperation> Operations { get; }

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
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (trace.StartChild("Create Distributed Transaction Response", TraceComponent.Batch, TraceLevel.Info))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract idempotency token from response headers, fallback to the request token if absent.
                Guid idempotencyToken = GetIdempotencyTokenFromHeaders(responseMessage.Headers, serverRequest.IdempotencyToken);

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
                        idempotencyToken);

                    // Validate results count matches operations count
                    if (response.results == null || response.results.Count != serverRequest.Operations.Count)
                    {
                        DefaultTrace.TraceWarning(
                            $"DTC response: result count ({response.results?.Count ?? 0}) differs from " +
                            $"operation count ({serverRequest.Operations.Count}).");

                        if (responseMessage.IsSuccessStatusCode)
                        {
                            SubStatusCodes wireSubStatusCode = responseMessage.Headers.SubStatusCode;

                            // Preserve the envelope isRetriable/DiagnosticString before disposing: a wrong
                            // result count is no more trustworthy than bad indices, and the retry signal is
                            // independent of the unusable payload. Matches the two unmappable fail-closed paths.
                            bool wireIsRetriable = response.IsRetriable;
                            string wireDiagnosticString = response.DiagnosticString;
                            response.Dispose();

                            return new DistributedTransactionResponse(
                                HttpStatusCode.InternalServerError,
                                wireSubStatusCode,
                                ClientResources.InvalidServerResponse,
                                responseMessage.Headers,
                                serverRequest.Operations,
                                serializer,
                                idempotencyToken,
                                wireIsRetriable)
                            {
                                DiagnosticString = wireDiagnosticString,
                            };
                        }

                        response.CreateAndPopulateResults(serverRequest.Operations);
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

            if (disposing)
            {
                DisposeResultStreams(this.results);
            }

            this.results = null;
            this.isDisposed = true;
        }

        private static void DisposeResultStreams(IEnumerable<DistributedTransactionOperationResult> results)
        {
            if (results == null)
            {
                return;
            }

            foreach (DistributedTransactionOperationResult result in results)
            {
                result.ResourceStream?.Dispose();
            }
        }

        private static Guid GetIdempotencyTokenFromHeaders(Headers headers, Guid fallbackToken)
        {
            if (headers != null &&
                headers.TryGetValue(HttpConstants.HttpHeaders.IdempotencyToken, out string tokenValue) &&
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
            CancellationToken cancellationToken)
        {
            List<DistributedTransactionOperationResult> results = new List<DistributedTransactionOperationResult>();
            bool isRetriable = false;
            string diagnosticString = null;

            JsonDocument responseJson;
            try
            {
                responseJson = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            }
            catch (JsonException jsonEx)
            {
                DefaultTrace.TraceWarning(
                    "DistributedTransactionResponse: failed to parse response body: {0}",
                    jsonEx.Message);

                if (responseMessage.IsSuccessStatusCode)
                {
                    return CreateDeserializationFailureResponse(responseMessage, serverRequest, serializer, idempotencyToken);
                }

                return null;
            }

            using (responseJson)
            {
                JsonElement root = responseJson.RootElement;

                if (DistributedTransactionOperationResult.TryGetProperty(root, DistributedTransactionSerializer.IsRetriable, out JsonElement isRetriableElement) &&
                    isRetriableElement.ValueKind == JsonValueKind.True)
                {
                    isRetriable = true;
                }

                if (DistributedTransactionOperationResult.TryGetProperty(root, DistributedTransactionSerializer.DiagnosticString, out JsonElement diagnosticStringElement) &&
                    diagnosticStringElement.ValueKind == JsonValueKind.String)
                {
                    diagnosticString = diagnosticStringElement.GetString();
                }

                // Parse operation results from "operationResponses" array.
                if (DistributedTransactionOperationResult.TryGetProperty(root, DistributedTransactionSerializer.OperationResponses, out JsonElement operationResponses) &&
                    operationResponses.ValueKind == JsonValueKind.Array)
                {
                    try
                    {
                        foreach (JsonElement operationElement in operationResponses.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            DistributedTransactionOperationResult operationResult = DistributedTransactionOperationResult.FromJson(operationElement);
                            results.Add(operationResult);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        DefaultTrace.TraceWarning(
                            "DistributedTransactionResponse: per-operation parse failed; discarding results. {0}",
                            jsonEx.Message);

                        DisposeResultStreams(results);
                        results.Clear();

                        // Preserve the coordinator's top-level isRetriable: it was parsed from the document
                        // root before this loop and is independent of any single operationResponses element.
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            return CreateDeserializationFailureResponse(responseMessage, serverRequest, serializer, idempotencyToken, diagnosticString, isRetriable);
                        }
                    }
                }
            }

            // Reorder so response[i] maps to request operation i via the per-operation 'index' (wire order
            // is not guaranteed). The indices must form a complete permutation of 0..n-1; otherwise the
            // payload is uninterpretable and we fail closed rather than surface misaligned data.
            if (results.Count > 0 && results.Count == serverRequest.Operations.Count)
            {
                DistributedTransactionOperationResult[] ordered = new DistributedTransactionOperationResult[results.Count];
                bool canReorder = true;
                foreach (DistributedTransactionOperationResult r in results)
                {
                    if (!r.HasIndex || r.Index < 0 || r.Index >= ordered.Length || ordered[r.Index] != null)
                    {
                        canReorder = false;
                        break;
                    }

                    ordered[r.Index] = r;
                }

                if (canReorder)
                {
                    results.Clear();
                    results.AddRange(ordered);
                }
                else
                {
                    DefaultTrace.TraceWarning(
                        "DistributedTransactionResponse: operation indices are not a complete permutation of 0..{0}; response is not interpretable.",
                        results.Count - 1);

                    DisposeResultStreams(results);
                    results.Clear();

                    // isRetriable is independent of the operation indices, so preserve it: combined with the
                    // idempotency token it lets the caller retry rather than fail terminally. On a success
                    // status fail closed with 500; on an error status leave results empty so the
                    // count-mismatch path pads with uniform error placeholders.
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        return CreateDeserializationFailureResponse(responseMessage, serverRequest, serializer, idempotencyToken, diagnosticString, isRetriable);
                    }
                }
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

            // Incorporate the coordinator's diagnosticString into the error message so it
            // surfaces in response.ErrorMessage and any exception message the caller builds.
            // Only merge on error responses — success responses must keep ErrorMessage null.
            string effectiveErrorMessage = responseMessage.ErrorMessage;
            bool isSuccessStatus = (int)finalStatusCode >= 200 && (int)finalStatusCode <= 299;
            if (!isSuccessStatus && !string.IsNullOrWhiteSpace(diagnosticString))
            {
                effectiveErrorMessage = string.IsNullOrWhiteSpace(effectiveErrorMessage)
                    ? diagnosticString
                    : $"{effectiveErrorMessage} ({diagnosticString})";
            }

            return new DistributedTransactionResponse(
                finalStatusCode,
                finalSubStatusCode,
                effectiveErrorMessage,
                responseMessage.Headers,
                serverRequest.Operations,
                serializer,
                idempotencyToken,
                isRetriable)
            {
                results = results,
                DiagnosticString = diagnosticString
            };
        }

        private void CreateAndPopulateResults(
            IReadOnlyList<DistributedTransactionOperation> operations)
        {
            // Dispose previously-parsed streams before replacing the list (the count-mismatch error path
            // reaches here with live results). Other paths already cleared results, so this is a no-op.
            DisposeResultStreams(this.results);

            this.results = new List<DistributedTransactionOperationResult>(operations.Count);

            for (int i = 0; i < operations.Count; i++)
            {
                // Leave per-op fields (SessionToken/PartitionKeyRangeId/ActivityId) null: this synthesized
                // path only runs when per-op results are missing/unmappable. The envelope ActivityId remains
                // on DistributedTransactionResponse.ActivityId.
                this.results.Add(new DistributedTransactionOperationResult(this.StatusCode)
                {
                    SubStatusCode = this.SubStatusCode,
                });
            }
        }

        /// <summary>
        /// Builds an InternalServerError response indicating the server replied with success but
        /// the SDK could not deserialize the response payload. Mirrors TransactionalBatch behavior.
        /// </summary>
        /// <remarks>
        /// <paramref name="diagnosticString"/> and the coordinator's <paramref name="isRetriable"/> verdict
        /// are independent of the unreadable payload, so both are preserved on the failure response.
        /// </remarks>
        private static DistributedTransactionResponse CreateDeserializationFailureResponse(
            ResponseMessage responseMessage,
            DistributedTransactionServerRequest serverRequest,
            CosmosSerializerCore serializer,
            Guid idempotencyToken,
            string diagnosticString = null,
            bool isRetriable = false)
        {
            DistributedTransactionResponse failedResponse = new DistributedTransactionResponse(
                HttpStatusCode.InternalServerError,
                SubStatusCodes.Unknown,
                ClientResources.ServerResponseDeserializationFailure,
                responseMessage.Headers,
                serverRequest.Operations,
                serializer,
                idempotencyToken,
                isRetriable)
            {
                DiagnosticString = diagnosticString,
            };

            failedResponse.CreateAndPopulateResults(serverRequest.Operations);
            return failedResponse;
        }
    }
}
