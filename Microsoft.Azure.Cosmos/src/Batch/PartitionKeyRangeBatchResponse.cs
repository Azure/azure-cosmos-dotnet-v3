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
    internal class PartitionKeyRangeBatchResponse : TransactionalBatchResponse
    {
        // Results sorted in the order operations had been added.
        private readonly TransactionalBatchOperationResult[] resultsByOperationIndex;
        private readonly TransactionalBatchResponse serverResponse;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionKeyRangeBatchResponse"/> class.
        /// </summary>
        /// <param name="originalOperationsCount">Original operations that generated the server responses.</param>
        /// <param name="serverResponse">Response from the server.</param>
        /// <param name="serializerCore">Serializer to deserialize response resource body streams.</param>
        internal PartitionKeyRangeBatchResponse(
            int originalOperationsCount,
            TransactionalBatchResponse serverResponse,
            CosmosSerializerCore serializerCore)
        {
            this.StatusCode = serverResponse.StatusCode;

            this.serverResponse = serverResponse;
            this.resultsByOperationIndex = new TransactionalBatchOperationResult[originalOperationsCount];

            StringBuilder errorMessageBuilder = new StringBuilder();
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
            this.Headers = serverResponse.Headers;

            if (!string.IsNullOrEmpty(serverResponse.ErrorMessage))
            {
                errorMessageBuilder.AppendFormat("{0}; ", serverResponse.ErrorMessage);
            }

            this.ErrorMessage = errorMessageBuilder.Length > 2 ? errorMessageBuilder.ToString(0, errorMessageBuilder.Length - 2) : null;
            this.Operations = itemBatchOperations;
            this.SerializerCore = serializerCore;
        }

        /// <summary>
        /// Gets the ActivityId that identifies the server request made to execute the batch request.
        /// </summary>
        public override string ActivityId => this.serverResponse.ActivityId;

        /// <inheritdoc />
        public override CosmosDiagnostics Diagnostics => this.serverResponse.Diagnostics;

        internal override CosmosSerializerCore SerializerCore { get; }

        /// <summary>
        /// Gets the number of operation results.
        /// </summary>
        public override int Count => this.resultsByOperationIndex.Length;

        /// <inheritdoc />
        public override TransactionalBatchOperationResult this[int index] => this.resultsByOperationIndex[index];

        /// <summary>
        /// Gets the result of the operation at the provided index in the batch - the returned result has a Resource of provided type.
        /// </summary>
        /// <typeparam name="T">Type to which the Resource in the operation result needs to be deserialized to, when present.</typeparam>
        /// <param name="index">0-based index of the operation in the batch whose result needs to be returned.</param>
        /// <returns>Result of batch operation that contains a Resource deserialized to specified type.</returns>
        public override TransactionalBatchOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            if (index >= this.Count)
            {
                throw new IndexOutOfRangeException();
            }

            TransactionalBatchOperationResult result = this.resultsByOperationIndex[index];

            T resource = default;
            if (result.ResourceStream != null)
            {
                resource = this.SerializerCore.FromStream<T>(result.ResourceStream);
            }

            return new TransactionalBatchOperationResult<T>(result, resource);
        }

        /// <summary>
        /// Gets an enumerator over the operation results.
        /// </summary>
        /// <returns>Enumerator over the operation results.</returns>
        public override IEnumerator<TransactionalBatchOperationResult> GetEnumerator()
        {
            foreach (TransactionalBatchOperationResult result in this.resultsByOperationIndex)
            {
                yield return result;
            }
        }

#if INTERNAL
        public 
#else
        internal
#endif
        override IEnumerable<string> GetActivityIds()
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
                this.serverResponse?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}