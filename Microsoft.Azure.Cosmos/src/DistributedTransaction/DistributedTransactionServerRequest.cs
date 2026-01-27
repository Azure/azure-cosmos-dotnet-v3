// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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

    internal class DistributedTransactionServerRequest
    {
        private readonly CosmosSerializerCore serializerCore;
        private MemorySpanResizer<byte> operationResizableWriteBuffer;
        private MemoryStream bodyStream;
        private int lastWrittenOperationIndex;

        private DistributedTransactionServerRequest(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializerCore)
        {
            this.Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
        }

        public IReadOnlyList<DistributedTransactionOperation> Operations { get; }

        public static async Task<DistributedTransactionServerRequest> CreateAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosSerializerCore serializerCore,
            CancellationToken cancellationToken)
        {
            DistributedTransactionServerRequest request = new DistributedTransactionServerRequest(operations, serializerCore);
            await request.CreateBodyStreamAsync(cancellationToken);
            return request;
        }

        public MemoryStream TransferBodyStream()
        {
            MemoryStream bodyStream = this.bodyStream;
            this.bodyStream = null;
            return bodyStream;
        }

        private async Task CreateBodyStreamAsync(CancellationToken cancellationToken)
        {
            int estimatedMaxOperationLength = 0;
            int approximateTotalLength = 0;

            foreach (DistributedTransactionOperation operation in this.Operations)
            {
                await operation.MaterializeResourceAsync(this.serializerCore, cancellationToken);

                operation.PartitionKeyJson ??= operation.PartitionKey.ToJsonString();

                int currentLength = operation.GetApproximateSerializedLength();
                estimatedMaxOperationLength = Math.Max(currentLength, estimatedMaxOperationLength);
                approximateTotalLength += currentLength;
            }

            const int operationSerializationOverheadOverEstimateInBytes = 200;
            this.bodyStream = new MemoryStream(approximateTotalLength + (operationSerializationOverheadOverEstimateInBytes * this.Operations.Count));
            this.operationResizableWriteBuffer = new MemorySpanResizer<byte>(estimatedMaxOperationLength + operationSerializationOverheadOverEstimateInBytes);

            Result r = await this.bodyStream.WriteRecordIOAsync(default, this.WriteOperation);
            Debug.Assert(r == Result.Success, "Failed to serialize distributed transaction request");

            this.bodyStream.Position = 0;
        }

        private Result WriteOperation(long index, out ReadOnlyMemory<byte> buffer)
        {
            if (index >= this.Operations.Count)
            {
                buffer = default;
                return Result.Success;
            }

            DistributedTransactionOperation operation = this.Operations[(int)index];

            RowBuffer row = new RowBuffer(this.operationResizableWriteBuffer.Memory.Length, this.operationResizableWriteBuffer);
            row.InitLayout(HybridRowVersion.V1, DistributedTransactionSchemaProvider.OperationLayout, DistributedTransactionSchemaProvider.LayoutResolver);
            Result r = RowWriter.WriteBuffer(ref row, operation, DistributedTransactionOperation.WriteOperation);
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
