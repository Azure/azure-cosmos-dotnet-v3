//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    internal sealed class EncryptionTransactionalBatchResponse : TransactionalBatchResponse
    {
        private readonly IReadOnlyList<TransactionalBatchOperationResult> results;
        private readonly TransactionalBatchResponse response;
        private readonly CosmosSerializer cosmosSerializer;
        private bool isDisposed = false;

        public EncryptionTransactionalBatchResponse(
            IReadOnlyList<TransactionalBatchOperationResult> results,
            TransactionalBatchResponse response,
            CosmosSerializer cosmosSerializer)
        {
            this.results = results;
            this.response = response;
            this.cosmosSerializer = cosmosSerializer;
        }

        public override TransactionalBatchOperationResult this[int index] => this.results[index];

        public override TransactionalBatchOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            TransactionalBatchOperationResult result = this.results[index];

            T resource = default;
            if (result.ResourceStream != null)
            {
                resource = this.cosmosSerializer.FromStream<T>(result.ResourceStream);
            }

            return new EncryptionTransactionalBatchOperationResult<T>(resource);
        }

        public override IEnumerator<TransactionalBatchOperationResult> GetEnumerator()
        {
            return this.results.GetEnumerator();
        }

        public override Headers Headers => this.response.Headers;

        public override string ActivityId => this.response.ActivityId;

        public override double RequestCharge => this.response.RequestCharge;

        public override TimeSpan? RetryAfter => this.response.RetryAfter;

        public override HttpStatusCode StatusCode => this.response.StatusCode;

        public override string ErrorMessage => this.response.ErrorMessage;

        public override bool IsSuccessStatusCode => this.response.IsSuccessStatusCode;

        public override int Count => this.results?.Count ?? 0;

        public override CosmosDiagnostics Diagnostics => this.response.Diagnostics;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;

                if (this.response != null)
                {
                    this.response.Dispose();
                }
            }
        }
    }
}