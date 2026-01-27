// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
    /// Represents result of a specific operation in distributed transaction.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    class DistributedTransactionOperationResult
    {
        internal DistributedTransactionOperationResult(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        internal DistributedTransactionOperationResult(DistributedTransactionOperationResult other)
        {
            this.Index = other.Index;
            this.StatusCode = other.StatusCode;
            this.SubStatusCode = other.SubStatusCode;
            this.ETag = other.ETag;
            this.ResourceStream = other.ResourceStream;
            this.SessionToken = other.SessionToken;
            this.RequestCharge = other.RequestCharge;
            this.RetryAfter = other.RetryAfter;
            this.ActivityId = other.ActivityId;
            this.Trace = other.Trace;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedTransactionOperationResult"/> class.
        /// This protected constructor is intended for use by derived classes.
        /// </summary>
        protected DistributedTransactionOperationResult()
        {
        }

        /// <summary>
        /// Gets the index of this operation within the distributed transaction.
        /// </summary>
        public virtual int Index { get; internal set; }

        /// <summary>
        /// Gets the HTTP status code returned by the operation.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the HTTP status code returned by the operation indicates success.
        /// </summary>
        public virtual bool IsSuccessStatusCode => (int)this.StatusCode >= 200 && (int)this.StatusCode <= 299;

        /// <summary>
        /// Gets the entity tag (ETag) associated with the operation result.
        /// The ETag is used for concurrency control and represents the version of the resource.
        /// </summary>
        public virtual string ETag { get; internal set; }

        /// <summary>
        /// Gets the session token associated with the operation result.
        /// </summary>
        public virtual string SessionToken { get; internal set; }

        /// <summary>
        /// Gets the resource stream associated with the operation result.
        /// The stream contains the raw response payload returned by the operation.
        /// </summary>
        public virtual Stream ResourceStream { get; internal set; }

        /// <summary>
        /// In case the operation is rate limited, indicates the time post which a retry can be attempted.
        /// </summary>
        public virtual TimeSpan RetryAfter { get; internal set; }

        /// <summary>
        /// Request charge in request units for the operation.
        /// </summary>
        internal virtual double RequestCharge { get; set; }

        internal virtual SubStatusCodes SubStatusCode { get; set; }

        /// <summary>
        /// ActivityId related to the operation.
        /// </summary>
        internal virtual string ActivityId { get; set; }

        internal ITrace Trace { get; set; }

        /// <summary>
        /// Reads a <see cref="DistributedTransactionOperationResult"/> from a HybridRow record.
        /// </summary>
        /// <param name="input">The input buffer containing the HybridRow record.</param>
        /// <param name="operationResult">The deserialized operation result.</param>
        /// <returns>The result of the read operation.</returns>
        internal static Result ReadOperationResult(ReadOnlyMemory<byte> input, out DistributedTransactionOperationResult operationResult)
        {
            RowBuffer row = new RowBuffer(input.Length);
            if (!row.ReadFrom(input.Span, HybridRowVersion.V1, DistributedTransactionSchemaProvider.LayoutResolver))
            {
                operationResult = null;
                return Result.Failure;
            }

            RowReader reader = new RowReader(ref row);
            Result result = ReadOperationResult(ref reader, out operationResult);
            
            if (result != Result.Success || operationResult.StatusCode == default)
            {
                return Result.Failure;
            }

            return Result.Success;
        }

        private static Result ReadOperationResult(ref RowReader reader, out DistributedTransactionOperationResult operationResult)
        {
            operationResult = new DistributedTransactionOperationResult();
            
            while (reader.Read())
            {
                Result r;
                switch (reader.Path)
                {
                    case "index":
                        r = reader.ReadUInt32(out uint index);
                        if (r != Result.Success) return r;
                        operationResult.Index = (int)index;
                        break;

                    case "statusCode":
                        r = reader.ReadInt32(out int statusCode);
                        if (r != Result.Success) return r;
                        operationResult.StatusCode = (HttpStatusCode)statusCode;
                        break;

                    case "subStatusCode":
                        r = reader.ReadInt32(out int subStatusCode);
                        if (r != Result.Success) return r;
                        operationResult.SubStatusCode = (SubStatusCodes)subStatusCode;
                        break;

                    case "eTag":
                        r = reader.ReadString(out string eTag);
                        if (r != Result.Success) return r;
                        operationResult.ETag = eTag;
                        break;

                    case "resourceBody":
                        r = reader.ReadBinary(out byte[] resourceBody);
                        if (r != Result.Success) return r;
                        operationResult.ResourceStream = new MemoryStream(resourceBody, 0, resourceBody.Length, writable: false, publiclyVisible: true);
                        break;

                    case "sessionToken":
                        r = reader.ReadString(out string sessionToken);
                        if (r != Result.Success) return r;
                        operationResult.SessionToken = sessionToken;
                        break;

                    case "requestCharge":
                        r = reader.ReadFloat64(out double requestCharge);
                        if (r != Result.Success) return r;
                        operationResult.RequestCharge = Math.Round(requestCharge, 2);
                        break;

                    case "retryAfterMilliseconds":
                        r = reader.ReadUInt32(out uint retryAfterMilliseconds);
                        if (r != Result.Success) return r;
                        operationResult.RetryAfter = TimeSpan.FromMilliseconds(retryAfterMilliseconds);
                        break;
                }
            }

            return Result.Success;
        }
    }
}
