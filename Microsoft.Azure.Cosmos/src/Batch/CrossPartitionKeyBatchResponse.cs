//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Response of a cross partition key batch request.
    /// </summary>
#pragma warning disable CA1710 // Identifiers should have correct suffix
    #if PREVIEW
    public
#else
    internal
#endif
    class CrossPartitionKeyBatchResponse : BatchResponse
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        // Results sorted in the order operations had been added.
        private readonly SortedList<int, BatchOperationResult> resultsByOperationIndex;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossPartitionKeyBatchResponse"/> class.
        /// </summary>
        /// <param name="statusCode">Completion status code of the batch request.</param>
        /// <param name="subStatusCode">Provides further details about why the batch was not processed.</param>
        /// <param name="operations">Operations that were supposed to be executed, but weren't.</param>
        /// <param name="errorMessage">The reason for failure if any.</param>
        // This constructor is expected to be used when the batch is not executed at all (if it is a bad request).
        internal CrossPartitionKeyBatchResponse(
            HttpStatusCode statusCode,
            SubStatusCodes subStatusCode,
            string errorMessage,
            IReadOnlyList<ItemBatchOperation> operations)
            : base(statusCode, subStatusCode, errorMessage, operations)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossPartitionKeyBatchResponse"/> class.
        /// </summary>
        /// <param name="serverResponses">Responses from the server.</param>
        /// <param name="serializer">Serializer to deserialize response resource body streams.</param>
        internal CrossPartitionKeyBatchResponse(IEnumerable<BatchResponse> serverResponses, CosmosSerializer serializer)
        {
            this.StatusCode = serverResponses.Any(r => r.StatusCode != HttpStatusCode.OK)
                ? (HttpStatusCode)BatchExecUtils.StatusCodeMultiStatus
                : HttpStatusCode.OK;

            this.ServerResponses = serverResponses;
            this.resultsByOperationIndex = new SortedList<int, BatchOperationResult>();

            StringBuilder errorMessageBuilder = new StringBuilder();
            List<string> activityIds = new List<string>();
            List<ItemBatchOperation> itemBatchOperations = new List<ItemBatchOperation>();
            foreach (BatchResponse serverResponse in serverResponses)
            {
                // We expect number of results == number of operations here
                for (int index = 0; index < serverResponse.Operations.Count; index++)
                {
                    int operationIndex = serverResponse.Operations[index].OperationIndex;
                    if (!this.resultsByOperationIndex.ContainsKey(operationIndex)
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

                activityIds.Add(serverResponse.ActivityId);
            }

            this.ActivityIds = activityIds;
            this.ErrorMessage = errorMessageBuilder.Length > 2 ? errorMessageBuilder.ToString(0, errorMessageBuilder.Length - 2) : null;
            this.Operations = itemBatchOperations;
            this.Serializer = serializer;
        }

        /// <summary>
        /// Gets the ActivityIds that identify the server requests made to execute the batch request.
        /// </summary>
#pragma warning disable CA1721 // Property names should not match get methods
        public virtual IEnumerable<string> ActivityIds { get; }
#pragma warning restore CA1721 // Property names should not match get methods

        /// <inheritdoc />
        public override string ActivityId => this.ActivityIds.First();

        internal override CosmosSerializer Serializer { get; }

        // for unit testing only
        internal IEnumerable<BatchResponse> ServerResponses { get; private set; }

        /// <summary>
        /// Gets the number of operation results.
        /// </summary>
        public override int Count => this.resultsByOperationIndex.Count;

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
            foreach (KeyValuePair<int, BatchOperationResult> pair in this.resultsByOperationIndex)
            {
                yield return pair.Value;
            }
        }

        internal override IEnumerable<string> GetActivityIds()
        {
            return this.ActivityIds;
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
                if (this.ServerResponses != null)
                {
                    foreach (BatchResponse response in this.ServerResponses)
                    {
                        response.Dispose();
                    }

                    this.ServerResponses = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}