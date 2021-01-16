//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a result for a specific operation that was part of a <see cref="TransactionalBatch"/> request.
    /// </summary>
    public class TransactionalBatchOperationResult
    {
        internal TransactionalBatchOperationResult(
            HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        internal TransactionalBatchOperationResult(TransactionalBatchOperationResult other)
        {
            this.StatusCode = other.StatusCode;
            this.SubStatusCode = other.SubStatusCode;
            this.ETag = other.ETag;
            this.ResourceStream = other.ResourceStream;
            this.RequestCharge = other.RequestCharge;
            this.RetryAfter = other.RetryAfter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchOperationResult"/> class.
        /// </summary>
        protected TransactionalBatchOperationResult()
        {
        }

        /// <summary>
        /// Gets the completion status of the operation.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current operation completed successfully.
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
        /// Gets the entity tag associated with the resource.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources.
        /// </remarks>
        public virtual string ETag { get; internal set; }

        /// <summary>
        /// Gets the content of the resource.
        /// </summary>
        /// <value>
        /// The content of the resource as a Stream.
        /// </value>
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// In case the operation is rate limited, indicates the time post which a retry can be attempted.
        /// </summary>
        public virtual TimeSpan RetryAfter { get; internal set; }

        /// <summary>
        /// Request charge in request units for the operation.
        /// </summary>
        internal virtual double RequestCharge { get; set; }

        /// <summary>
        /// Gets detail on the completion status of the operation.
        /// </summary>
        internal virtual SubStatusCodes SubStatusCode { get; set; } 

        internal ITrace Trace { get; set; }

        internal static Result ReadOperationResult(ReadOnlyMemory<byte> input, out TransactionalBatchOperationResult batchOperationResult)
        {
            RowBuffer row = new RowBuffer(input.Length);
            if (!row.ReadFrom(
                input.Span,
                HybridRowVersion.V1,
                BatchSchemaProvider.BatchLayoutResolver))
            {
                batchOperationResult = null;
                return Result.Failure;
            }

            RowReader reader = new RowReader(ref row);
            Result result = TransactionalBatchOperationResult.ReadOperationResult(ref reader, out batchOperationResult);
            if (result != Result.Success)
            {
                return result;
            }

            // Ensure the mandatory fields were populated
            if (batchOperationResult.StatusCode == default)
            {
                return Result.Failure;
            }

            return Result.Success;
        }

        private static Result ReadOperationResult(ref RowReader reader, out TransactionalBatchOperationResult batchOperationResult)
        {
            batchOperationResult = new TransactionalBatchOperationResult();
            while (reader.Read())
            {
                Result r;
                switch (reader.Path)
                {
                    case "statusCode":
                        r = reader.ReadInt32(out int statusCode);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        batchOperationResult.StatusCode = (HttpStatusCode)statusCode;
                        break;

                    case "subStatusCode":
                        r = reader.ReadInt32(out int subStatusCode);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        batchOperationResult.SubStatusCode = (SubStatusCodes)subStatusCode;
                        break;

                    case "eTag":
                        r = reader.ReadString(out string eTag);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        batchOperationResult.ETag = eTag;
                        break;

                    case "resourceBody":
                        r = reader.ReadBinary(out byte[] resourceBody);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        batchOperationResult.ResourceStream = new MemoryStream(
                            buffer: resourceBody, index: 0, count: resourceBody.Length, writable: false, publiclyVisible: true);
                        break;

                    case "requestCharge":
                        r = reader.ReadFloat64(out double requestCharge);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        // Round request charge to 2 decimals on the operation results
                        // similar to how we round them for the full response.
                        batchOperationResult.RequestCharge = Math.Round(requestCharge, 2);
                        break;

                    case "retryAfterMilliseconds":
                        r = reader.ReadUInt32(out uint retryAfterMilliseconds);
                        if (r != Result.Success)
                        {
                            return r;
                        }

                        batchOperationResult.RetryAfter = TimeSpan.FromMilliseconds(retryAfterMilliseconds);
                        break;
                }
            }

            return Result.Success;
        }

        internal ResponseMessage ToResponseMessage()
        {
            Headers headers = new Headers()
            {
                SubStatusCode = this.SubStatusCode,
                ETag = this.ETag,
                RetryAfter = this.RetryAfter,
                RequestCharge = this.RequestCharge,
            };

            ResponseMessage responseMessage = new ResponseMessage(
                statusCode: this.StatusCode,
                requestMessage: null,
                headers: headers,
                cosmosException: null,
                trace: this.Trace ?? NoOpTrace.Singleton)
            {
                Content = this.ResourceStream
            };

            return responseMessage;
        }
    }

    /// <summary>
    /// Represents a result for a specific operation that is part of a batch.
    /// </summary>
    /// <typeparam name="T">The type of the Resource which this class wraps.</typeparam>
#pragma warning disable SA1402 // File may only contain a single type
    public class TransactionalBatchOperationResult<T> : TransactionalBatchOperationResult
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchOperationResult{T}"/> class.
        /// </summary>
        /// <param name="result">BatchOperationResult with stream resource.</param>
        /// <param name="resource">Deserialized resource.</param>
        internal TransactionalBatchOperationResult(TransactionalBatchOperationResult result, T resource)
            : base(result)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalBatchOperationResult{T}"/> class.
        /// </summary>
        protected TransactionalBatchOperationResult()
        {
        }

        /// <summary>
        /// Gets the content of the resource.
        /// </summary>
        public virtual T Resource { get; set; }
    }
}