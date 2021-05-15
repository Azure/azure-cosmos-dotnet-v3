//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "To be fixed, tracked in issue #1575")]
    internal sealed class EncryptionTransactionalBatch : TransactionalBatch
    {
        private readonly CosmosSerializer cosmosSerializer;
        private readonly EncryptionContainer encryptionContainer;
        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatch(
            TransactionalBatch transactionalBatch,
            EncryptionContainer encryptionContainer,
            CosmosSerializer cosmosSerializer)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
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
                EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (encryptionSettings.PropertiesToEncrypt.Any())
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        encryptionSettings,
                        diagnosticsContext,
                        cancellationToken: default)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
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
                EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (encryptionSettings.PropertiesToEncrypt.Any())
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        encryptionSettings,
                        diagnosticsContext,
                        default)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
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
                EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (encryptionSettings.PropertiesToEncrypt.Any())
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        encryptionSettings,
                        diagnosticsContext,
                        cancellationToken: default)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
            }

            this.transactionalBatch = this.transactionalBatch.UpsertItemStream(
                streamPayload,
                requestOptions);

            return this;
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            return await this.ExecuteAsync(requestOptions: null, cancellationToken: cancellationToken);
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            TransactionalBatchRequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync.WithRequestOptions"))
            {
                TransactionalBatchResponse response = null;

                EncryptionSettings encryptionSettings = await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
                if (!encryptionSettings.PropertiesToEncrypt.Any())
                {
                    return await this.transactionalBatch.ExecuteAsync(requestOptions, cancellationToken);
                }
                else
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

                    encryptionSettings.SetRequestHeaders(clonedRequestOptions);
                    response = await this.transactionalBatch.ExecuteAsync(clonedRequestOptions, cancellationToken);
                }

                // FIXME this should check for BadRequest StatusCode too, requires a service fix to return 400 instead of -1 which is currently returned.
                if (string.Equals(response.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
                {
                    await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(
                        obsoleteEncryptionSettings: encryptionSettings,
                        cancellationToken: cancellationToken);

                    throw new CosmosException(
                        "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + response.ErrorMessage,
                        HttpStatusCode.BadRequest,
                        int.Parse(Constants.IncorrectContainerRidSubStatus),
                        response.Headers.ActivityId,
                        response.Headers.RequestCharge);
                }

                return await this.DecryptTransactionalBatchResponseAsync(
                    response,
                    encryptionSettings,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        public override TransactionalBatch PatchItem(
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            TransactionalBatchPatchItemRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }

        private async Task<TransactionalBatchResponse> DecryptTransactionalBatchResponseAsync(
            TransactionalBatchResponse response,
            EncryptionSettings encryptionSettings,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return response;
            }

            List<TransactionalBatchOperationResult> decryptedTransactionalBatchOperationResults = new List<TransactionalBatchOperationResult>();

            for (int index = 0; index < response.Count; index++)
            {
                TransactionalBatchOperationResult result = response[index];

                if (response.IsSuccessStatusCode && result.ResourceStream != null)
                {
                    Stream decryptedStream = await EncryptionProcessor.DecryptAsync(
                        result.ResourceStream,
                        encryptionSettings,
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
    }
}
