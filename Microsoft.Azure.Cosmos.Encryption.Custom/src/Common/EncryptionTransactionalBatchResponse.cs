//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Threading;

    internal sealed class EncryptionTransactionalBatchResponse : TransactionalBatchResponse
    {
        private readonly IReadOnlyList<TransactionalBatchOperationResult> results;
        private readonly TransactionalBatchResponse response;
        private readonly CosmosSerializer cosmosSerializer;
        private int isDisposed;

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
            if (!disposing || Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            {
                return;
            }

            ExceptionDispatchInfo cleanupException = null;
            if (this.results != null)
            {
                foreach (TransactionalBatchOperationResult result in this.results)
                {
                    if (result is EncryptionTransactionalBatchOperationResult decryptedResult)
                    {
                        try
                        {
                            decryptedResult.DisposeDecryptedResourceStream();
                        }
                        catch (Exception exception)
                        {
                            cleanupException ??= ExceptionDispatchInfo.Capture(exception);
                        }
                    }
                }
            }

            try
            {
                this.response?.Dispose();
            }
            catch (Exception exception)
            {
                cleanupException ??= ExceptionDispatchInfo.Capture(exception);
            }

            cleanupException?.Throw();
        }
    }
}