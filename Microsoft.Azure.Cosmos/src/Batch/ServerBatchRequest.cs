//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal abstract class ServerBatchRequest
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly int maxBodyLength;

        private readonly int maxOperationCount;

        private readonly CosmosSerializerCore serializerCore;

        private ArraySegment<ItemBatchOperation> operations;

        private MemorySpanResizer<byte> operationResizableWriteBuffer;

        private MemoryStream bodyStream;

        private long bodyStreamPositionBeforeWritingCurrentRecord;

        private bool shouldDeleteLastWrittenRecord;

        private int lastWrittenOperationIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerBatchRequest"/> class.
        /// </summary>
        /// <param name="maxBodyLength">Maximum length allowed for the request body.</param>
        /// <param name="maxOperationCount">Maximum number of operations allowed in the request.</param>
        /// <param name="serializerCore">Serializer to serialize user provided objects to JSON.</param>
        protected ServerBatchRequest(int maxBodyLength, int maxOperationCount, CosmosSerializerCore serializerCore)
        {
            this.maxBodyLength = maxBodyLength;
            this.maxOperationCount = maxOperationCount;
            this.serializerCore = serializerCore;
        }

        public IReadOnlyList<ItemBatchOperation> Operations => this.operations;

        /// <summary>
        /// Returns the body Stream.
        /// Caller is responsible for disposing it after use.
        /// </summary>
        /// <returns>Body stream.</returns>
        public MemoryStream TransferBodyStream()
        {
            MemoryStream bodyStream = this.bodyStream;
            this.bodyStream = null;
            return bodyStream;
        }

        /// <summary>
        /// Adds as many operations as possible from the provided list of operations
        /// in the list order while having the body stream not exceed maxBodySize.
        /// </summary>
        /// <param name="operations">Operations to be added; read-only.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <param name="ensureContinuousOperationIndexes">Whether to stop adding operations to the request once there is non-continuity in the operation indexes.</param>
        /// <returns>Any pending operations that were not included in the request.</returns>
        protected async Task<ArraySegment<ItemBatchOperation>> CreateBodyStreamAsync(
            ArraySegment<ItemBatchOperation> operations,
            CancellationToken cancellationToken,
            bool ensureContinuousOperationIndexes = false)
        {
            int estimatedMaxOperationLength = 0;
            int approximateTotalLength = 0;

            int previousOperationIndex = -1;
            int materializedCount = 0;
            foreach (ItemBatchOperation operation in operations)
            {
                if (ensureContinuousOperationIndexes && previousOperationIndex != -1 && operation.OperationIndex != previousOperationIndex + 1)
                {
                    break;
                }

                await operation.TransformAndMaterializeResourceAsync(this.serializerCore, cancellationToken);
                materializedCount++;

                previousOperationIndex = operation.OperationIndex;

                int currentLength = operation.GetApproximateSerializedLength();
                estimatedMaxOperationLength = Math.Max(currentLength, estimatedMaxOperationLength);

                approximateTotalLength += currentLength;
                if (approximateTotalLength > this.maxBodyLength)
                {
                    break;
                }

                if (materializedCount == this.maxOperationCount)
                {
                    break;
                }
            }

            this.operations = new ArraySegment<ItemBatchOperation>(operations.Array, operations.Offset, materializedCount);

            const int operationSerializationOverheadOverEstimateInBytes = 200;
            this.bodyStream = new MemoryStream(approximateTotalLength + (operationSerializationOverheadOverEstimateInBytes * materializedCount));
            this.operationResizableWriteBuffer = new MemorySpanResizer<byte>(estimatedMaxOperationLength + operationSerializationOverheadOverEstimateInBytes);

            Result r = await this.bodyStream.WriteRecordIOAsync(default(Segment), this.WriteOperation);
            Debug.Assert(r == Result.Success, "Failed to serialize batch request");

            this.bodyStream.Position = 0;

            if (this.shouldDeleteLastWrittenRecord)
            {
                this.bodyStream.SetLength(this.bodyStreamPositionBeforeWritingCurrentRecord);
                this.operations = new ArraySegment<ItemBatchOperation>(operations.Array, operations.Offset, this.lastWrittenOperationIndex);
            }
            else
            {
                this.operations = new ArraySegment<ItemBatchOperation>(operations.Array, operations.Offset, this.lastWrittenOperationIndex + 1);
            }

            int overflowOperations = operations.Count - this.operations.Count;
            return new ArraySegment<ItemBatchOperation>(operations.Array, this.operations.Count + operations.Offset, overflowOperations);
        }

        private Result WriteOperation(long index, out ReadOnlyMemory<byte> buffer)
        {
            if (this.bodyStream.Length > this.maxBodyLength)
            {
                // If there is only one operation within the request, we will keep it even if it
                // exceeds the maximum size allowed for the body.
                if (index > 1)
                {
                    this.shouldDeleteLastWrittenRecord = true;
                }

                buffer = default(ReadOnlyMemory<byte>);
                return Result.Success;
            }

            this.bodyStreamPositionBeforeWritingCurrentRecord = this.bodyStream.Length;

            if (index >= this.operations.Count)
            {
                buffer = default(ReadOnlyMemory<byte>);
                return Result.Success;
            }

            ItemBatchOperation operation = this.operations.Array[this.operations.Offset + (int)index];

            RowBuffer row = new RowBuffer(this.operationResizableWriteBuffer.Memory.Length, this.operationResizableWriteBuffer);
            row.InitLayout(HybridRowVersion.V1, BatchSchemaProvider.BatchOperationLayout, BatchSchemaProvider.BatchLayoutResolver);
            Result r = RowWriter.WriteBuffer(ref row, operation, ItemBatchOperation.WriteOperation);
            if (r != Result.Success)
            {
                buffer = null;
                return r;
            }

            this.lastWrittenOperationIndex = (int)index;
            buffer = this.operationResizableWriteBuffer.Memory.Slice(0, row.Length);
            return Result.Success;
        }
    }
}