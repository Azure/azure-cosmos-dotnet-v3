// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class SinglePartitionKeyServerBatchRequest : ServerBatchRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SinglePartitionKeyServerBatchRequest"/> class.
        /// Single partition key server request.
        /// </summary>
        /// <param name="partitionKey">Partition key that applies to all operations in this request.</param>
        /// <param name="maxBodyLength">Maximum length allowed for the request body.</param>
        /// <param name="maxOperationCount">Maximum number of operations allowed in the request.</param>
        /// <param name="serializer">Serializer to serialize user provided objects to JSON.</param>
        private SinglePartitionKeyServerBatchRequest(
            PartitionKey? partitionKey,
            int maxBodyLength,
            int maxOperationCount,
            CosmosSerializer serializer)
            : base(maxBodyLength, maxOperationCount, serializer)
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
        /// <param name="maxBodyLength">Desired maximum length of the request body.</param>
        /// <param name="maxOperationCount">Maximum number of operations allowed in the request.</param>
        /// <param name="serializer">Serializer to serialize user provided objects to JSON.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A newly created instance of <see cref="SinglePartitionKeyServerBatchRequest"/>.</returns>
        public static async Task<SinglePartitionKeyServerBatchRequest> CreateAsync(
            PartitionKey? partitionKey,
            ArraySegment<ItemBatchOperation> operations,
            int maxBodyLength,
            int maxOperationCount,
            CosmosSerializer serializer,
            CancellationToken cancellationToken)
        {
            SinglePartitionKeyServerBatchRequest request = new SinglePartitionKeyServerBatchRequest(partitionKey, maxBodyLength, maxOperationCount, serializer);
            await request.CreateBodyStreamAsync(operations, cancellationToken);
            return request;
        }
    }
}
