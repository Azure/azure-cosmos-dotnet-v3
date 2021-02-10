//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Response of a <see cref="TransactionalBatch"/> request.
    /// </summary>
#pragma warning disable CA1710 // Identifiers should have correct suffix
    public class TransactionalBatchResponse : IReadOnlyList<TransactionalBatchOperationResult>, IDisposable
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        private List<TransactionalBatchOperationResult> results;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchResponse"/> class.
        /// This method is intended to be used only when a response from the server is not available.
        /// </summary>
        /// <param name="statusCode">Indicates why the batch was not processed.</param>
        /// <param name="subStatusCode">Provides further details about why the batch was not processed.</param>
        /// <param name="errorMessage">The reason for failure.</param>
        /// <param name="operations">Operations that were to be executed.</param>
        /// <param name="trace">Diagnostics for the operation</param>
        internal TransactionalBatchResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            IReadOnlyList<ItemBatchOperation> operations,
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
        /// Initializes a new instance of the <see cref="TransactionalBatchResponse"/> class.
        /// </summary>
        protected TransactionalBatchResponse()
        {
        }

        private TransactionalBatchResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            Headers headers,
            ITrace trace,
            IReadOnlyList<ItemBatchOperation> operations,
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
        /// Gets the current HTTP headers.
        /// </summary>
        public virtual Headers Headers { get; internal set; }

        /// <summary>
        /// Gets the ActivityId that identifies the server request made to execute the batch.
        /// </summary>
        public virtual string ActivityId => this.Headers?.ActivityId;

        /// <summary>
        /// Gets the request charge for the batch request.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge => this.Headers?.RequestCharge ?? 0;

        /// <summary>
        /// Gets the amount of time to wait before retrying this or any other request within Cosmos container or collection due to throttling.
        /// </summary>
        public virtual TimeSpan? RetryAfter => this.Headers?.RetryAfter;

        /// <summary>
        /// Gets the completion status code of the batch request.
        /// </summary>
        /// <value>The request completion status code.</value>
        public virtual HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// Gets the reason for failure of the batch request.
        /// </summary>
        /// <value>The reason for failure, if any.</value>
        public virtual string ErrorMessage { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the batch was processed.
        /// </summary>
        public virtual bool IsSuccessStatusCode
        {
            get
            {
                int statusCodeInt = (int)this.StatusCode;
                return statusCodeInt >= 200 && statusCodeInt <= 299;
            }
        }

        /// <summary>
        /// Gets the number of operation results.
        /// </summary>
        public virtual int Count => this.results?.Count ?? 0;

        /// <summary>
        /// Gets the cosmos diagnostic information for the current request to Azure Cosmos DB service
        /// </summary>
        public virtual CosmosDiagnostics Diagnostics { get; }

        internal virtual SubStatusCodes SubStatusCode { get; }

        internal virtual CosmosSerializerCore SerializerCore { get; }

        internal IReadOnlyList<ItemBatchOperation> Operations { get; set; }

        /// <summary>
        /// Gets the result of the operation at the provided index in the batch.
        /// </summary>
        /// <param name="index">0-based index of the operation in the batch whose result needs to be returned.</param>
        /// <returns>Result of operation at the provided index in the batch.</returns>
        public virtual TransactionalBatchOperationResult this[int index] => this.results[index];

        /// <summary>
        /// Gets the result of the operation at the provided index in the batch - the returned result has a Resource of provided type.
        /// </summary>
        /// <typeparam name="T">Type to which the Resource in the operation result needs to be deserialized to, when present.</typeparam>
        /// <param name="index">0-based index of the operation in the batch whose result needs to be returned.</param>
        /// <returns>Result of batch operation that contains a Resource deserialized to specified type.</returns>
        public virtual TransactionalBatchOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            TransactionalBatchOperationResult result = this.results[index];

            T resource = default;
            if (result.ResourceStream != null)
            {
                resource = this.SerializerCore.FromStream<T>(result.ResourceStream);
            }

            return new TransactionalBatchOperationResult<T>(result, resource);
        }

        /// <summary>
        /// Gets an enumerator over the operation results.
        /// </summary>
        /// <returns>Enumerator over the operation results.</returns>
        public virtual IEnumerator<TransactionalBatchOperationResult> GetEnumerator()
        {
            return this.results.GetEnumerator();
        }

        /// <summary>
        /// Gets all the Activity IDs associated with the response.
        /// </summary>
        /// <returns>An enumerable that contains the Activity IDs.</returns>
#if INTERNAL
        public 
#else
        internal
#endif
        virtual IEnumerable<string> GetActivityIds()
        {
            yield return this.ActivityId;
        }

        /// <summary>
        /// Disposes the current <see cref="TransactionalBatchResponse"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal static async Task<TransactionalBatchResponse> FromResponseMessageAsync(
            ResponseMessage responseMessage,
            ServerBatchRequest serverRequest,
            CosmosSerializerCore serializer,
            bool shouldPromoteOperationStatus,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace createResponseTrace = trace.StartChild("Create Trace", TraceComponent.Batch, TraceLevel.Info))
            {
                using (responseMessage)
                {
                    TransactionalBatchResponse response = null;
                    if (responseMessage.Content != null)
                    {
                        Stream content = responseMessage.Content;

                        // Shouldn't be the case practically, but handle it for safety.
                        if (!responseMessage.Content.CanSeek)
                        {
                            content = new MemoryStream();
                            await responseMessage.Content.CopyToAsync(content);
                        }

                        if (content.ReadByte() == (int)HybridRowVersion.V1)
                        {
                            content.Position = 0;
                            response = await TransactionalBatchResponse.PopulateFromContentAsync(
                                content,
                                responseMessage,
                                serverRequest,
                                serializer,
                                trace,
                                shouldPromoteOperationStatus);

                            if (response == null)
                            {
                                // Convert any payload read failures as InternalServerError
                                response = new TransactionalBatchResponse(
                                    HttpStatusCode.InternalServerError,
                                    SubStatusCodes.Unknown,
                                    ClientResources.ServerResponseDeserializationFailure,
                                    responseMessage.Headers,
                                    trace,
                                    serverRequest.Operations,
                                    serializer);
                            }
                        }
                    }

                    if (response == null)
                    {
                        response = new TransactionalBatchResponse(
                            responseMessage.StatusCode,
                            responseMessage.Headers.SubStatusCode,
                            responseMessage.ErrorMessage,
                            responseMessage.Headers,
                            trace,
                            serverRequest.Operations,
                            serializer);
                    }

                    if (response.results == null || response.results.Count != serverRequest.Operations.Count)
                    {
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            // Server should be guaranteeing number of results equal to operations when
                            // batch request is successful - so fail as InternalServerError if this is not the case.
                            response = new TransactionalBatchResponse(
                                HttpStatusCode.InternalServerError,
                                SubStatusCodes.Unknown,
                                ClientResources.InvalidServerResponse,
                                responseMessage.Headers,
                                trace,
                                serverRequest.Operations,
                                serializer);
                        }

                        // When the overall response status code is TooManyRequests, propagate the RetryAfter into the individual operations.
                        int retryAfterMilliseconds = 0;

                        if ((int)responseMessage.StatusCode == (int)StatusCodes.TooManyRequests)
                        {
                            if (!responseMessage.Headers.TryGetValue(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, out string retryAfter) ||
                                retryAfter == null ||
                                !int.TryParse(retryAfter, out retryAfterMilliseconds))
                            {
                                retryAfterMilliseconds = 0;
                            }
                        }

                        response.CreateAndPopulateResults(serverRequest.Operations, trace, retryAfterMilliseconds);
                    }

                    return response;
                }
            }
        }

        private void CreateAndPopulateResults(IReadOnlyList<ItemBatchOperation> operations, ITrace trace, int retryAfterMilliseconds = 0)
        {
            this.results = new List<TransactionalBatchOperationResult>();
            for (int i = 0; i < operations.Count; i++)
            {
                TransactionalBatchOperationResult result = new TransactionalBatchOperationResult(this.StatusCode)
                {
                    SubStatusCode = this.SubStatusCode,
                    RetryAfter = TimeSpan.FromMilliseconds(retryAfterMilliseconds),
                };

                result.Trace = trace;

                this.results.Add(result);
            }
        }

        private static async Task<TransactionalBatchResponse> PopulateFromContentAsync(
            Stream content,
            ResponseMessage responseMessage,
            ServerBatchRequest serverRequest,
            CosmosSerializerCore serializer,
            ITrace trace,
            bool shouldPromoteOperationStatus)
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();

            // content is ensured to be seekable in caller.
            int resizerInitialCapacity = (int)content.Length;

            Result res = await content.ReadRecordIOAsync(
                (Func<ReadOnlyMemory<byte>, Result>)(record =>
                {
                    Result r = TransactionalBatchOperationResult.ReadOperationResult(record, out TransactionalBatchOperationResult operationResult);
                    if (r != Result.Success)
                    {
                        return r;
                    }

                    operationResult.Trace = trace;

                    results.Add(operationResult);
                    return r;
                }),
                resizer: new MemorySpanResizer<byte>(resizerInitialCapacity));

            if (res != Result.Success)
            {
                return null;
            }

            HttpStatusCode responseStatusCode = responseMessage.StatusCode;
            SubStatusCodes responseSubStatusCode = responseMessage.Headers.SubStatusCode;

            // Promote the operation error status as the Batch response error status if we have a MultiStatus response
            // to provide users with status codes they are used to.
            if ((int)responseMessage.StatusCode == (int)StatusCodes.MultiStatus
                && shouldPromoteOperationStatus)
            {
                foreach (TransactionalBatchOperationResult result in results)
                {
                    if ((int)result.StatusCode != (int)StatusCodes.FailedDependency && (int)result.StatusCode >= (int)StatusCodes.StartingErrorCode)
                    {
                        responseStatusCode = result.StatusCode;
                        responseSubStatusCode = result.SubStatusCode;
                        break;
                    }
                }
            }

            TransactionalBatchResponse response = new TransactionalBatchResponse(
                responseStatusCode,
                responseSubStatusCode,
                responseMessage.ErrorMessage,
                responseMessage.Headers,
                trace,
                serverRequest.Operations,
                serializer)
            {
                results = results
            };
            return response;
        }

        /// <summary>
        /// Disposes the disposable members held by this class.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose managed resources or not.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Operations != null)
                {
                    foreach (ItemBatchOperation operation in this.Operations)
                    {
                        operation.Dispose();
                    }

                    this.Operations = null;
                }
            }
        }
    }
}