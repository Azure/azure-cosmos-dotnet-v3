//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Util methods for batch requests.
    /// </summary>
    internal static class BatchExecUtils
    {
        // Using the same buffer size as the Stream.DefaultCopyBufferSize
        private const int BufferSize = 81920;

        /// <summary>
        /// Converts a Stream to a Memory{byte} wrapping a byte array.
        /// </summary>
        /// <param name="stream">Stream to be converted to bytes.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>A Memory{byte}.</returns>
        public static async Task<Memory<byte>> StreamToMemoryAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            if (stream.CanSeek)
            {
                // Some derived implementations of MemoryStream (such as versions of RecyclableMemoryStream prior to 1.2.2 that we may be using)
                // return an incorrect response from TryGetBuffer. Use TryGetBuffer only on the MemoryStream type and not derived types.
                if (stream is MemoryStream memStream
                     && memStream.GetType() == typeof(MemoryStream)
                     && memStream.TryGetBuffer(out ArraySegment<byte> memBuffer))
                {
                    return memBuffer;
                }

                byte[] bytes = new byte[stream.Length];
                int sum = 0;
                int count;
                while ((count = await stream.ReadAsync(bytes, sum, bytes.Length - sum, cancellationToken)) > 0)
                {
                    sum += count;
                }

                return bytes;
            }
            else
            {
                int bufferSize = BatchExecUtils.BufferSize;
                byte[] buffer = new byte[bufferSize];

                using (MemoryStream memoryStream = new MemoryStream(bufferSize)) // using bufferSize as initial capacity as well
                {
                    int sum = 0;
                    int count;
                    while ((count = await stream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
                    {
                        sum += count;

#pragma warning disable VSTHRD103 // Call async methods when in an async method
                        memoryStream.Write(buffer, 0, count);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                    }

                    return new Memory<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                }
            }
        }

        public static void EnsureValid(
            IReadOnlyList<ItemBatchOperation> operations,
            RequestOptions batchOptions)
        {
            string errorMessage = BatchExecUtils.IsValid(operations, batchOptions);

            if (errorMessage != null)
            {
                throw new ArgumentException(errorMessage);
            }
        }

        internal static string IsValid(
            IReadOnlyList<ItemBatchOperation> operations,
            RequestOptions batchOptions)
        {
            string errorMessage = null;

            if (operations.Count == 0)
            {
                errorMessage = ClientResources.BatchNoOperations;
            }

            if (errorMessage == null && batchOptions != null)
            {
                if (batchOptions.IfMatchEtag != null || batchOptions.IfNoneMatchEtag != null)
                {
                    errorMessage = ClientResources.BatchRequestOptionNotSupported;
                }
            }

            if (errorMessage == null)
            {
                foreach (ItemBatchOperation operation in operations)
                {
                    if (operation.RequestOptions != null
                        && operation.RequestOptions.Properties != null
                        && (operation.RequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj)
                            | operation.RequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkStrObj)
                            | operation.RequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.PartitionKey, out object pkStrObj)))
                    {
                        byte[] epk = epkObj as byte[];
                        string partitionKeyJsonString = pkStrObj as string;
                        if ((epk == null && partitionKeyJsonString == null) || !(epkStrObj is string epkStr))
                        {
                            errorMessage = string.Format(
                                ClientResources.EpkPropertiesPairingExpected,
                                WFConstants.BackendHeaders.EffectivePartitionKey,
                                WFConstants.BackendHeaders.EffectivePartitionKeyString);
                        }

                        if (operation.PartitionKey != null && !operation.RequestOptions.IsEffectivePartitionKeyRouting)
                        {
                            errorMessage = ClientResources.PKAndEpkSetTogether;
                        }
                    }
                }
            }

            return errorMessage;
        }

        public static string GetPartitionKeyRangeId(PartitionKey partitionKey, PartitionKeyDefinition partitionKeyDefinition, Routing.CollectionRoutingMap collectionRoutingMap)
        {
            string effectivePartitionKey = partitionKey.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
            return collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePartitionKey).Id;
        }
    }
}