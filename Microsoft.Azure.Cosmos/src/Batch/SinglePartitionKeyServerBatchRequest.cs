// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class SinglePartitionKeyServerBatchRequest : ServerBatchRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SinglePartitionKeyServerBatchRequest"/> class.
        /// Single partition key server request.
        /// </summary>
        /// <param name="partitionKey">Partition key that applies to all operations in this request.</param>
        /// <param name="serializerCore">Serializer to serialize user provided objects to JSON.</param>
        private SinglePartitionKeyServerBatchRequest(
            PartitionKey? partitionKey,
            CosmosSerializerCore serializerCore)
            : base(maxBodyLength: int.MaxValue,
                  maxOperationCount: int.MaxValue,
                  serializerCore: serializerCore)
        {
            this.PartitionKey = partitionKey;
        }

        /// <summary>
        ///  PartitionKey that applies to all operations in this request.
        /// </summary>
        public PartitionKey? PartitionKey { get; }

        /// <summary>
        /// Creates an instance of <see cref="SinglePartitionKeyServerBatchRequest"/>.
        /// The body of the request is populated with operations till it reaches the provided maxBodyLength.
        /// </summary>
        /// <param name="partitionKey">Partition key of the request.</param>
        /// <param name="operations">Operations to be added into this batch request.</param>
        /// <param name="serializerCore">Serializer to serialize user provided objects to JSON.</param>
        /// <param name="trace">The trace.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A newly created instance of <see cref="SinglePartitionKeyServerBatchRequest"/>.</returns>
        public static async Task<SinglePartitionKeyServerBatchRequest> CreateAsync(
            PartitionKey? partitionKey,
            ArraySegment<ItemBatchOperation> operations,
            CosmosSerializerCore serializerCore,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (trace.StartChild("Create Batch Request", TraceComponent.Batch, TraceLevel.Info))
            {
                SinglePartitionKeyServerBatchRequest request = new SinglePartitionKeyServerBatchRequest(partitionKey, serializerCore);
                await request.CreateBodyStreamAsync(operations, cancellationToken);
                return request;
            }
        }
    }
}
