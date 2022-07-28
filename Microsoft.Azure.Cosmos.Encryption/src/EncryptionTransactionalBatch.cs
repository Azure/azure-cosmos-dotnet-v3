﻿//------------------------------------------------------------
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
        private readonly EncryptionDiagnosticsContext encryptionDiagnosticsContext;
        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatch(
            TransactionalBatch transactionalBatch,
            EncryptionContainer encryptionContainer,
            CosmosSerializer cosmosSerializer)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
            this.encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            this.encryptionDiagnosticsContext.Begin(Constants.DiagnosticsEncryptOperation);
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
            EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (encryptionSettings.PropertiesToEncrypt.Any())
            {
               streamPayload = EncryptionProcessor.EncryptAsync(
                   streamPayload,
                   encryptionSettings,
                   operationDiagnostics: null,
                   cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
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
            EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            id = this.encryptionContainer.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            this.transactionalBatch = this.transactionalBatch.DeleteItem(
                id,
                requestOptions);

            return this;
        }

        public override TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            id = this.encryptionContainer.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

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
            EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (encryptionSettings.PropertiesToEncrypt.Any())
            {
                streamPayload = EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    encryptionSettings,
                    operationDiagnostics: null,
                    cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                id = this.encryptionContainer.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
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
            EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (encryptionSettings.PropertiesToEncrypt.Any())
            {
                streamPayload = EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    encryptionSettings,
                    operationDiagnostics: null,
                    cancellationToken: default)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
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
            EncryptionSettings encryptionSettings = await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            TransactionalBatchResponse response;

            this.encryptionDiagnosticsContext.End();

            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return await this.transactionalBatch.ExecuteAsync(requestOptions, cancellationToken);
            }
            else
            {
                TransactionalBatchRequestOptions clonedRequestOptions = requestOptions != null
                    ? (TransactionalBatchRequestOptions)requestOptions.ShallowCopy()
                    : new TransactionalBatchRequestOptions();

                encryptionSettings.SetRequestHeaders(clonedRequestOptions);
                response = await this.transactionalBatch.ExecuteAsync(clonedRequestOptions, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest && string.Equals(response.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    obsoleteEncryptionSettings: encryptionSettings,
                    cancellationToken: cancellationToken);

                // no access to the encryption diagnostics. Just pass empty encryption diagnostics for now.
                EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
                EncryptionCosmosDiagnostics encryptionDiagnostics = new EncryptionCosmosDiagnostics(
                    response.Diagnostics,
                    encryptionDiagnosticsContext.EncryptContent,
                    encryptionDiagnosticsContext.DecryptContent,
                    encryptionDiagnosticsContext.TotalProcessingDuration);

                throw new EncryptionCosmosException(
                    "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Retrying may fix the issue. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + response.ErrorMessage,
                    HttpStatusCode.BadRequest,
                    int.Parse(Constants.IncorrectContainerRidSubStatus),
                    response.Headers.ActivityId,
                    response.Headers.RequestCharge,
                    encryptionDiagnostics);
            }

            return await this.DecryptTransactionalBatchResponseAsync(
                response,
                encryptionSettings,
                cancellationToken);
        }

        public override TransactionalBatch PatchItem(
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            TransactionalBatchPatchItemRequestOptions requestOptions = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (patchOperations == null ||
                !patchOperations.Any())
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }

            EncryptionSettings encryptionSettings = this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(
                obsoleteEncryptionSettings: null,
                cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            EncryptionDiagnosticsContext encryptionDiagnosticsContext = new EncryptionDiagnosticsContext();
            List<PatchOperation> encryptedPatchOperations = this.encryptionContainer.EncryptPatchOperationsAsync(
                patchOperations,
                encryptionSettings,
                encryptionDiagnosticsContext,
                cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            id = this.encryptionContainer.CheckIfIdIsEncryptedAndGetEncryptedIdAsync(id, encryptionSettings, cancellationToken: default)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            this.transactionalBatch = this.transactionalBatch.PatchItem(
                id,
                encryptedPatchOperations,
                requestOptions);

            return this;
        }

        private async Task<TransactionalBatchResponse> DecryptTransactionalBatchResponseAsync(
            TransactionalBatchResponse response,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return response;
            }

            List<TransactionalBatchOperationResult> decryptedTransactionalBatchOperationResults = new List<TransactionalBatchOperationResult>();

            this.encryptionDiagnosticsContext.Begin(Constants.DiagnosticsDecryptOperation);

            foreach (TransactionalBatchOperationResult result in response)
            {
                if (response.IsSuccessStatusCode && result.ResourceStream != null)
                {
                    Stream decryptedStream = await EncryptionProcessor.DecryptAsync(
                        result.ResourceStream,
                        encryptionSettings,
                        operationDiagnostics: null,
                        cancellationToken);

                    decryptedTransactionalBatchOperationResults.Add(new EncryptionTransactionalBatchOperationResult(result, decryptedStream));
                }
                else
                {
                    decryptedTransactionalBatchOperationResults.Add(result);
                }
            }

            this.encryptionDiagnosticsContext.End();
            EncryptionCosmosDiagnostics encryptionDiagnostics = new EncryptionCosmosDiagnostics(
                response.Diagnostics,
                encryptContent: this.encryptionDiagnosticsContext.EncryptContent,
                decryptContent: this.encryptionDiagnosticsContext.DecryptContent,
                this.encryptionDiagnosticsContext.TotalProcessingDuration);

            return new EncryptionTransactionalBatchResponse(
                decryptedTransactionalBatchOperationResults,
                response,
                this.cosmosSerializer,
                encryptionDiagnostics);
        }
    }
}
