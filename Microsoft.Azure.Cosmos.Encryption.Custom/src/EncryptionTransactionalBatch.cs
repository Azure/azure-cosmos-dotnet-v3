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
        private readonly JsonProcessor defaultJsonProcessor;
        private readonly List<JsonProcessor?> operationJsonProcessorOverrides = new ();
        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatch(
            TransactionalBatch transactionalBatch,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer,
            JsonProcessor defaultJsonProcessor)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
            this.defaultJsonProcessor = defaultJsonProcessor;
        }

        public override TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                requestOptions = this.SelectAndSanitize(
                    requestOptions,
                    this.defaultJsonProcessor,
                    storeSelectedProcessor: false,
                    out _,
                    out JsonProcessor? jsonProcessorOverride);
                this.transactionalBatch = this.transactionalBatch.CreateItem(
                    item,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

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
            bool isEncryptedWrite = requestOptions is EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions: not null,
            };
            requestOptions = this.SelectAndSanitize(
                requestOptions,
                isEncryptedWrite ? JsonProcessor.Newtonsoft : this.defaultJsonProcessor,
                storeSelectedProcessor: isEncryptedWrite,
                out JsonProcessor jsonProcessor,
                out JsonProcessor? jsonProcessorOverride);

            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        jsonProcessor,
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            this.transactionalBatch = this.transactionalBatch.CreateItemStream(
                streamPayload,
                requestOptions);
            this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

            return this;
        }

        public override TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            requestOptions = this.SelectAndSanitize(
                requestOptions,
                this.defaultJsonProcessor,
                storeSelectedProcessor: false,
                out _,
                out JsonProcessor? jsonProcessorOverride);
            this.transactionalBatch = this.transactionalBatch.DeleteItem(
                id,
                requestOptions);
            this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

            return this;
        }

        public override TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            requestOptions = this.SelectAndSanitize(
                requestOptions,
                this.defaultJsonProcessor,
                storeSelectedProcessor: false,
                out _,
                out JsonProcessor? jsonProcessorOverride);
            this.transactionalBatch = this.transactionalBatch.ReadItem(
                id,
                requestOptions);
            this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

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
                requestOptions = this.SelectAndSanitize(
                    requestOptions,
                    this.defaultJsonProcessor,
                    storeSelectedProcessor: false,
                    out _,
                    out JsonProcessor? jsonProcessorOverride);
                this.transactionalBatch = this.transactionalBatch.ReplaceItem(
                    id,
                    item,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

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
            bool isEncryptedWrite = requestOptions is EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions: not null,
            };
            requestOptions = this.SelectAndSanitize(
                requestOptions,
                isEncryptedWrite ? JsonProcessor.Newtonsoft : this.defaultJsonProcessor,
                storeSelectedProcessor: isEncryptedWrite,
                out JsonProcessor jsonProcessor,
                out JsonProcessor? jsonProcessorOverride);

            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        jsonProcessor,
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            this.transactionalBatch = this.transactionalBatch.ReplaceItemStream(
                id,
                streamPayload,
                requestOptions);
            this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

            return this;
        }

        public override TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                requestOptions = this.SelectAndSanitize(
                    requestOptions,
                    this.defaultJsonProcessor,
                    storeSelectedProcessor: false,
                    out _,
                    out JsonProcessor? jsonProcessorOverride);
                this.transactionalBatch = this.transactionalBatch.UpsertItem(
                    item,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

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
            bool isEncryptedWrite = requestOptions is EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions: not null,
            };
            requestOptions = this.SelectAndSanitize(
                requestOptions,
                isEncryptedWrite ? JsonProcessor.Newtonsoft : this.defaultJsonProcessor,
                storeSelectedProcessor: isEncryptedWrite,
                out JsonProcessor jsonProcessor,
                out JsonProcessor? jsonProcessorOverride);

            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        jsonProcessor,
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            this.transactionalBatch = this.transactionalBatch.UpsertItemStream(
                streamPayload,
                requestOptions);
            this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);

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
                    this.defaultJsonProcessor,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            TransactionalBatchRequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            requestOptions = requestOptions.SelectAndSanitizeJsonProcessor(
                this.defaultJsonProcessor,
                out JsonProcessor jsonProcessor,
                out _);
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync.WithRequestOptions"))
            {
                TransactionalBatchResponse response = await this.transactionalBatch.ExecuteAsync(requestOptions, cancellationToken);
                return await this.DecryptTransactionalBatchResponseAsync(
                    response,
                    jsonProcessor,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<TransactionalBatchResponse> DecryptTransactionalBatchResponseAsync(
            TransactionalBatchResponse response,
            JsonProcessor batchJsonProcessor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            List<TransactionalBatchOperationResult> decryptedTransactionalBatchOperationResults = new ();
            EncryptionTransactionalBatchResponse decryptedResponse = new (
                decryptedTransactionalBatchOperationResults,
                response,
                this.cosmosSerializer);
            int operationIndex = 0;

            try
            {
                foreach (TransactionalBatchOperationResult result in response)
                {
                    Stream resourceStream = result.ResourceStream;
                    if (response.IsSuccessStatusCode && resourceStream != null)
                    {
                        JsonProcessor jsonProcessor = operationIndex < this.operationJsonProcessorOverrides.Count
                            ? this.operationJsonProcessorOverrides[operationIndex] ?? batchJsonProcessor
                            : batchJsonProcessor;
                        (Stream decryptedStream, _) = await EncryptionProcessor.DecryptAsync(
                            resourceStream,
                            this.encryptor,
                            jsonProcessor,
                            legacyFallback: true,
                            diagnosticsContext,
                            cancellationToken);

                        decryptedTransactionalBatchOperationResults.Add(
                            ReferenceEquals(resourceStream, decryptedStream)
                                ? result
                                : new EncryptionTransactionalBatchOperationResult(result, decryptedStream));
                    }
                    else
                    {
                        decryptedTransactionalBatchOperationResults.Add(result);
                    }

                    operationIndex++;
                }

                return decryptedResponse;
            }
            catch
            {
                decryptedResponse.Dispose();
                throw;
            }
        }

        private TransactionalBatchItemRequestOptions SelectAndSanitize(
            TransactionalBatchItemRequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            bool storeSelectedProcessor,
            out JsonProcessor jsonProcessor,
            out JsonProcessor? jsonProcessorOverride)
        {
            requestOptions = requestOptions.SelectAndSanitizeJsonProcessor(
                defaultJsonProcessor,
                out jsonProcessor,
                out bool hasOverride);
            jsonProcessorOverride = hasOverride || storeSelectedProcessor ? jsonProcessor : null;
            return requestOptions;
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
