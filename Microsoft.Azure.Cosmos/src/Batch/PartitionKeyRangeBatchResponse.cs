//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Response of a cross partition key batch request.
    /// </summary>
    internal class PartitionKeyRangeBatchResponse : BatchResponse
    {
        // Results sorted in the order operations had been added.
        private readonly BatchOperationResult[] resultsByOperationIndex;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionKeyRangeBatchResponse"/> class.
        /// </summary>
        /// <param name="statusCode">Completion status code of the batch request.</param>
        /// <param name="subStatusCode">Provides further details about why the batch was not processed.</param>
        /// <param name="operations">Operations that were supposed to be executed, but weren't.</param>
        /// <param name="errorMessage">The reason for failure if any.</param>
        // This constructor is expected to be used when the batch is not executed at all (if it is a bad request).
        internal PartitionKeyRangeBatchResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            IReadOnlyList<ItemBatchOperation> operations)
            : base(statusCode, subStatusCode, errorMessage, operations)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionKeyRangeBatchResponse"/> class.
        /// </summary>
        /// <param name="originalOperationsCount">Original operations that generated the server responses.</param>
        /// <param name="serverResponse">Response from the server.</param>
        /// <param name="serializer">Serializer to deserialize response resource body streams.</param>
        internal PartitionKeyRangeBatchResponse(
            int originalOperationsCount,
            BatchResponse serverResponse,
            CosmosSerializer serializer)
        {
            this.StatusCode = serverResponse.StatusCode;

            this.ServerResponse = serverResponse;
            this.resultsByOperationIndex = new BatchOperationResult[originalOperationsCount];

            StringBuilder errorMessageBuilder = new StringBuilder();
            List<string> activityIds = new List<string>();
            List<ItemBatchOperation> itemBatchOperations = new List<ItemBatchOperation>();
            // We expect number of results == number of operations here
            for (int index = 0; index < serverResponse.Operations.Count; index++)
            {
                int operationIndex = serverResponse.Operations[index].OperationIndex;
                if (this.resultsByOperationIndex[operationIndex] == null
                    || this.resultsByOperationIndex[operationIndex].StatusCode == (HttpStatusCode)StatusCodes.TooManyRequests)
                {
                    this.resultsByOperationIndex[operationIndex] = serverResponse[index];
                }
            }

            itemBatchOperations.AddRange(serverResponse.Operations);
            this.RequestCharge += serverResponse.RequestCharge;

            if (!string.IsNullOrEmpty(serverResponse.ErrorMessage))
            {
                errorMessageBuilder.AppendFormat("{0}; ", serverResponse.ErrorMessage);
            }

            this.ActivityId = serverResponse.ActivityId;
            this.ErrorMessage = errorMessageBuilder.Length > 2 ? errorMessageBuilder.ToString(0, errorMessageBuilder.Length - 2) : null;
            this.Operations = itemBatchOperations;
            this.Serializer = serializer;
        }

        /// <summary>
        /// Gets the ActivityId that identifies the server request made to execute the batch request.
        /// </summary>
        public override string ActivityId { get; }

        internal override CosmosSerializer Serializer { get; }

        // for unit testing only
        internal BatchResponse ServerResponse { get; private set; }

        /// <summary>
        /// Gets the number of operation results.
        /// </summary>
        public override int Count => this.resultsByOperationIndex.Length;

        /// <inheritdoc />
        public override BatchOperationResult this[int index] => this.resultsByOperationIndex[index];

        /// <summary>
        /// Gets the result of the operation at the provided index in the batch - the returned result has a Resource of provided type.
        /// </summary>
        /// <typeparam name="T">Type to which the Resource in the operation result needs to be deserialized to, when present.</typeparam>
        /// <param name="index">0-based index of the operation in the batch whose result needs to be returned.</param>
        /// <returns>Result of batch operation that contains a Resource deserialized to specified type.</returns>
        public override BatchOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            if (index >= this.Count)
            {
                throw new IndexOutOfRangeException();
            }

            BatchOperationResult result = this.resultsByOperationIndex[index];

            T resource = default(T);
            if (result.ResourceStream != null)
            {
                resource = this.Serializer.FromStream<T>(result.ResourceStream);
            }

            return new BatchOperationResult<T>(result, resource);
        }

        /// <summary>
        /// Gets an enumerator over the operation results.
        /// </summary>
        /// <returns>Enumerator over the operation results.</returns>
        public override IEnumerator<BatchOperationResult> GetEnumerator()
        {
            foreach (BatchOperationResult result in this.resultsByOperationIndex)
            {
                yield return result;
            }
        }

        internal override IEnumerable<string> GetActivityIds()
        {
            return new string[1] { this.ActivityId };
        }

        /// <summary>
        /// Disposes the disposable members held.
        /// </summary>
        /// <param name="disposing">Indicates whether to dispose managed resources or not.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;
                this.ServerResponse?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}