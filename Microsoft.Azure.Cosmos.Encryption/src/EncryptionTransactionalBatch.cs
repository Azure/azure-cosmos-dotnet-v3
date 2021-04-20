//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "To be fixed, tracked in issue #1575")]
    internal sealed class EncryptionTransactionalBatch : TransactionalBatch
    {
        private readonly CosmosSerializer cosmosSerializer;
        private readonly EncryptionProcessor encryptionProcessor;
        private readonly EncryptionContainer encryptionContainer;
        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatch(
            TransactionalBatch transactionalBatch,
            EncryptionContainer encryptionContainer,
            CosmosSerializer cosmosSerializer)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.encryptionProcessor = encryptionContainer.EncryptionProcessor;
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
        }

        public override TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
            return this.CreateItemStream(
                itemStream,
                requestOptions);
        }

        public override TransactionalBatch CreateItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("EncryptItemStream"))
            {
                (streamPayload, _, _ ) = this.encryptionProcessor.EncryptAsync(
                    streamPayload,
                    diagnosticsContext,
                    cancellationToken: default).Result;
            }

            this.transactionalBatch = this.transactionalBatch.CreateItemStream(
                streamPayload,
                requestOptions);

            return this;
        }

        public override TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            this.transactionalBatch = this.transactionalBatch.DeleteItem(
                id,
                requestOptions);

            return this;
        }

        public override TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            this.transactionalBatch = this.transactionalBatch.ReadItem(
                id,
                requestOptions);

            return this;
        }

        public override TransactionalBatch ReplaceItem<T>(
            string id,
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
            return this.ReplaceItemStream(
                id,
                itemStream,
                requestOptions);
        }

        public override TransactionalBatch ReplaceItemStream(
            string id,
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("EncryptItemStream"))
            {
                (streamPayload, _, _) = this.encryptionProcessor.EncryptAsync(
                    streamPayload,
                    diagnosticsContext,
                    default).Result;
            }

            this.transactionalBatch = this.transactionalBatch.ReplaceItemStream(
                id,
                streamPayload,
                requestOptions);

            return this;
        }

        public override TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
            return this.UpsertItemStream(
                itemStream,
                requestOptions);
        }

        public override TransactionalBatch UpsertItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            using (diagnosticsContext.CreateScope("EncryptItemStream"))
            {
                (streamPayload, _, _) = this.encryptionProcessor.EncryptAsync(
                    streamPayload,
                    diagnosticsContext,
                    cancellationToken: default).Result;
            }

            this.transactionalBatch = this.transactionalBatch.UpsertItemStream(
                streamPayload,
                requestOptions);

            return this;
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync"))
            {
                TransactionalBatchRequestOptions requestOptions = new TransactionalBatchRequestOptions
                {
                    AddRequestHeaders = this.encryptionContainer.AddHeaders,
                };

                TransactionalBatchResponse response = await this.transactionalBatch.ExecuteAsync(requestOptions, cancellationToken);

                foreach (TransactionalBatchOperationResult transactionalBatchOperationResult in response)
                {
                    if (transactionalBatchOperationResult.StatusCode != System.Net.HttpStatusCode.Created
                        && transactionalBatchOperationResult.StatusCode != System.Net.HttpStatusCode.OK
                        && string.Equals(response.Headers.Get("x-ms-substatus"), "1024"))
                    {
                        await this.encryptionContainer.InitEncryptionContainerCacheIfNotInitAsync(cancellationToken, shouldForceRefresh: true);

                        throw new CosmosException(
                           "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + response.ErrorMessage,
                           response.StatusCode,
                           1024,
                           response.Headers.ActivityId,
                           response.Headers.RequestCharge);
                    }
                }

                return await this.DecryptTransactionalBatchResponseAsync(
                        response,
                        diagnosticsContext,
                        cancellationToken);
            }
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            TransactionalBatchRequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync.WithRequestOptions"))
            {
                TransactionalBatchRequestOptions clonedRequestOptions;
                if (requestOptions != null)
                {
                    clonedRequestOptions = (TransactionalBatchRequestOptions)requestOptions.ShallowCopy();
                }
                else
                {
                    clonedRequestOptions = new TransactionalBatchRequestOptions();
                }

                clonedRequestOptions.AddRequestHeaders = this.encryptionContainer.AddHeaders;

                TransactionalBatchResponse response = await this.transactionalBatch.ExecuteAsync(clonedRequestOptions, cancellationToken);

                foreach (TransactionalBatchOperationResult transactionalBatchOperationResult in response)
                {
                    if (transactionalBatchOperationResult.StatusCode != System.Net.HttpStatusCode.Created
                        && transactionalBatchOperationResult.StatusCode != System.Net.HttpStatusCode.OK
                        && string.Equals(response.Headers.Get("x-ms-substatus"), "1024"))
                    {
                        await this.encryptionContainer.InitEncryptionContainerCacheIfNotInitAsync(cancellationToken, shouldForceRefresh: true);

                        throw new CosmosException(
                            "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + response.ErrorMessage,
                            response.StatusCode,
                            1024,
                            response.Headers.ActivityId,
                            response.Headers.RequestCharge);
                    }
                }

                return await this.DecryptTransactionalBatchResponseAsync(
                    response,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<TransactionalBatchResponse> DecryptTransactionalBatchResponseAsync(
            TransactionalBatchResponse response,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            List<TransactionalBatchOperationResult> decryptedTransactionalBatchOperationResults = new List<TransactionalBatchOperationResult>();

            for (int index = 0; index < response.Count; index++)
            {
                TransactionalBatchOperationResult result = response[index];

                if (response.IsSuccessStatusCode && result.ResourceStream != null)
                {
                    Stream decryptedStream = await this.encryptionProcessor.DecryptAsync(
                        result.ResourceStream,
                        diagnosticsContext,
                        cancellationToken);

                    result = new EncryptionTransactionalBatchOperationResult(response[index], decryptedStream);
                }

                decryptedTransactionalBatchOperationResults.Add(result);
            }

            return new EncryptionTransactionalBatchResponse(
                decryptedTransactionalBatchOperationResults,
                response,
                this.cosmosSerializer);
        }

        public override TransactionalBatch PatchItem(
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            TransactionalBatchPatchItemRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }
    }
}
