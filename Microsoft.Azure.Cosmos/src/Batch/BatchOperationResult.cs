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
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a result for a specific operation that was part of a batch request.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class BatchOperationResult
    {
        internal BatchOperationResult(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        internal BatchOperationResult(BatchOperationResult other)
        {
            this.StatusCode = other.StatusCode;
            this.SubStatusCode = other.SubStatusCode;
            this.ETag = other.ETag;
            this.ResourceStream = other.ResourceStream;
            this.RetryAfter = other.RetryAfter;
        }

        private BatchOperationResult()
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
        /// The content of the resource as a MemoryStream.
        /// </value>
        public virtual MemoryStream ResourceStream { get; internal set; }

        /// <summary>
        /// In case the operation is rate limited, indicates the time post which a retry can be attempted.
        /// </summary>
        public virtual TimeSpan RetryAfter { get; internal set; }

        /// <summary>
        /// Gets detail on the completion status of the operation.
        /// </summary>
        internal virtual SubStatusCodes SubStatusCode { get; set; }

        internal static Result ReadOperationResult(Memory<byte> input, out BatchOperationResult batchOperationResult)
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
            Result result = BatchOperationResult.ReadOperationResult(ref reader, out batchOperationResult);
            if (result != Result.Success)
            {
                return result;
            }

            // Ensure the mandatory fields were populated
            if (batchOperationResult.StatusCode == default(HttpStatusCode))
            {
                return Result.Failure;
            }

            return Result.Success;
        }

        private static Result ReadOperationResult(ref RowReader reader, out BatchOperationResult batchOperationResult)
        {
            batchOperationResult = new BatchOperationResult();
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
    }

    /// <summary>
    /// Represents a result for a specific operation that is part of a batch.
    /// </summary>
    /// <typeparam name="T">The type of the Resource which this class wraps.</typeparam>
#pragma warning disable SA1402 // File may only contain a single type
#if PREVIEW
    public
#else
    internal
#endif
    class BatchOperationResult<T> : BatchOperationResult
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchOperationResult{T}"/> class.
        /// </summary>
        /// <param name="result">CosmosBatchOperationResult with stream resource.</param>
        /// <param name="resource">Deserialized resource.</param>
        internal BatchOperationResult(BatchOperationResult result, T resource)
            : base(result)
        {
            this.Resource = resource;
        }

        /// <summary>
        /// Gets the content of the resource.
        /// </summary>
        public virtual T Resource { get; set; }
    }
}