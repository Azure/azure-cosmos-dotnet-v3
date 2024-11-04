//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "To be fixed, tracked in issue #1575")]
    internal sealed class EncryptionTransactionalBatchStream : TransactionalBatch
    {
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly StreamManager streamManager;

        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatchStream(
            TransactionalBatch transactionalBatch,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            StreamManager streamManager)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
            this.streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        }

        public override TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.CreateItem(
                    item,
                    requestOptions);

                return this;
            }

#if SDKPROJECTREF
            using Stream itemStream = this.streamManager.CreateStream();
            this.cosmosSerializer.ToStreamAsync<T>(item, itemStream, CancellationToken.None).GetAwaiter().GetResult();
#else
            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
#endif
            return this.CreateItemStream(
                itemStream,
                requestOptions);
        }

        public override TransactionalBatch CreateItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    Stream temp = this.streamManager.CreateStream();
                    EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        temp,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        diagnosticsContext,
                        cancellationToken: default).GetAwaiter().GetResult();
                    streamPayload = temp;
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
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.ReplaceItem(
                    id,
                    item,
                    requestOptions);

                return this;
            }
#if SDKPROJECTREF
            using Stream itemStream = this.streamManager.CreateStream();
            this.cosmosSerializer.ToStreamAsync<T>(item, itemStream, CancellationToken.None).GetAwaiter().GetResult();
#else
            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
#endif
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
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                Stream temp = this.streamManager.CreateStream();
                EncryptionProcessor.EncryptAsync(
                    streamPayload,
                    temp,
                    this.encryptor,
                    encryptionItemRequestOptions.EncryptionOptions,
                    diagnosticsContext,
                    cancellationToken: default).GetAwaiter().GetResult();
                streamPayload = temp;
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
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.UpsertItem(
                    item,
                    requestOptions);

                return this;
            }

#if SDKPROJECTREF
            using Stream itemStream = this.streamManager.CreateStream();
            this.cosmosSerializer.ToStreamAsync<T>(item, itemStream, CancellationToken.None).GetAwaiter().GetResult();
#else
            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
#endif
            return this.UpsertItemStream(
                itemStream,
                requestOptions);
        }

        public override TransactionalBatch UpsertItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    Stream temp = this.streamManager.CreateStream();
                    EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        temp,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        diagnosticsContext,
                        cancellationToken: default).GetAwaiter().GetResult();
                    streamPayload = temp;
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
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync"))
            {
                TransactionalBatchResponse response = await this.transactionalBatch.ExecuteAsync(cancellationToken);
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
                TransactionalBatchResponse response = await this.transactionalBatch.ExecuteAsync(requestOptions, cancellationToken);
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
            List<TransactionalBatchOperationResult> decryptedTransactionalBatchOperationResults = new ();

            foreach (TransactionalBatchOperationResult result in response)
            {
                if (response.IsSuccessStatusCode && result.ResourceStream != null)
                {
                    Stream decryptedStream = this.streamManager.CreateStream();
                    _ = await EncryptionProcessor.DecryptAsync(
                        result.ResourceStream,
                        decryptedStream,
                        this.encryptor,
                        diagnosticsContext,
                        JsonProcessor.Stream,
                        cancellationToken);

                    decryptedTransactionalBatchOperationResults.Add(new EncryptionTransactionalBatchOperationResult(result, decryptedStream));
                }
                else
                {
                    decryptedTransactionalBatchOperationResults.Add(result);
                }
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
#endif