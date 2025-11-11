//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
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
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatch(
            TransactionalBatch transactionalBatch,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
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

            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
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
                    streamPayload = this.EncryptStreamSync(
                        streamPayload,
                        encryptionItemRequestOptions.EncryptionOptions,
                        requestOptions,
                        diagnosticsContext,
                        "CreateItemStream");
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
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = this.EncryptStreamSync(
                        streamPayload,
                        encryptionItemRequestOptions.EncryptionOptions,
                        requestOptions,
                        diagnosticsContext,
                        "ReplaceItemStream");
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
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.UpsertItem(
                    item,
                    requestOptions);

                return this;
            }

            Stream itemStream = this.cosmosSerializer.ToStream<T>(item);
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
                    streamPayload = this.EncryptStreamSync(
                        streamPayload,
                        encryptionItemRequestOptions.EncryptionOptions,
                        requestOptions,
                        diagnosticsContext,
                        "UpsertItemStream");
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
                    Stream decryptedStream = await this.DecryptStreamAsync(
                        result.ResourceStream,
                        diagnosticsContext,
                        cancellationToken,
                        "DecryptTransactionalBatchResponse");

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

        private Stream EncryptStreamSync(
            Stream input,
            EncryptionOptions encryptionOptions,
            RequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            string tag)
        {
            Stream output = MemoryStreamPool.GetStream(tag);
            EncryptionProcessor.EncryptAsync(
                input,
                output,
                this.encryptor,
                encryptionOptions,
                requestOptions,
                diagnosticsContext,
                cancellationToken: default).GetAwaiter().GetResult();
            output.Position = 0;
            return output;
        }

        private async Task<Stream> DecryptStreamAsync(
            Stream input,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken,
            string tag)
        {
            Stream output = MemoryStreamPool.GetStream(tag);
            bool success = false;
            try
            {
                await EncryptionProcessor.DecryptAsync(
                    input,
                    output,
                    this.encryptor,
                    diagnosticsContext,
                    requestOptions: null,
                    cancellationToken);
                output.Position = 0;
                success = true;
                return output;
            }
            finally
            {
                if (!success)
                {
#if NET8_0_OR_GREATER
                    await output.DisposeAsync();
#else
                    output.Dispose();
#endif
                }
            }
        }
    }
}
