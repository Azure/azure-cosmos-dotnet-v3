//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// <see cref="RequestOptions"/> that applies to an operation within a <see cref="TransactionalBatch"/>.
    /// </summary>
    public class TransactionalBatchItemRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the indexing directive (Include or Exclude) for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The indexing directive to use with a request.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
        public IndexingDirective? IndexingDirective { get; set; }

        /// <summary>
        /// Gets or sets the boolean to only return the headers and status code in
        /// the Cosmos DB response for write item operations like Create, Upsert and Replace.
        /// This removes the resource from the response. This reduces networking and CPU load by not sending
        /// the resource back over the network and serializing it on the client.
        /// </summary>
        /// <remarks>
        /// This is optimal for workloads where the returned resource is not used.
        /// </remarks>
        public bool? EnableContentResponseOnWrite { get; set; }

        internal static TransactionalBatchItemRequestOptions FromItemRequestOptions(ItemRequestOptions itemRequestOptions)
        {
            if (itemRequestOptions == null)
            {
                return null;
            }

            TransactionalBatchItemRequestOptions batchItemRequestOptions = new TransactionalBatchItemRequestOptions
            {
                IndexingDirective = itemRequestOptions.IndexingDirective,
                IfMatchEtag = itemRequestOptions.IfMatchEtag,
                IfNoneMatchEtag = itemRequestOptions.IfNoneMatchEtag,
                Properties = itemRequestOptions.Properties,
                EnableContentResponseOnWrite = itemRequestOptions.EnableContentResponseOnWrite,
                IsEffectivePartitionKeyRouting = itemRequestOptions.IsEffectivePartitionKeyRouting
            };
            return batchItemRequestOptions;
        }

        internal virtual Result WriteRequestProperties(ref RowWriter writer, bool pkWritten)
        {
            if (this.Properties == null)
            {
                return Result.Success;
            }

            if (this.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out object binaryIdObj)
                && binaryIdObj is byte[] binaryId)
            {
                Result r = writer.WriteBinary("binaryId", binaryId);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (this.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj)
                && epkObj is byte[] epk)
            {
                Result r = writer.WriteBinary("effectivePartitionKey", epk);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (!pkWritten && this.Properties.TryGetValue(HttpConstants.HttpHeaders.PartitionKey, out object pkStrObj)
                && pkStrObj is string pkString)
            {
                Result r = writer.WriteString("partitionKey", pkString);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (this.Properties.TryGetValue(WFConstants.BackendHeaders.TimeToLiveInSeconds, out object ttlObj)
                && ttlObj is string ttlStr && int.TryParse(ttlStr, out int ttl))
            {
                Result r = writer.WriteInt32("timeToLiveInSeconds", ttl);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            return Result.Success;
        }

        internal virtual int GetRequestPropertiesSerializationLength()
        {
            if (this.Properties == null)
            {
                return 0;
            }

            int length = 0;
            if (this.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out object binaryIdObj)
                && binaryIdObj is byte[] binaryId)
            {
                length += binaryId.Length;
            }

            if (this.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj)
                && epkObj is byte[] epk)
            {
                length += epk.Length;
            }

            return length;
        }
    }
}