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
        private readonly object operationStateLock = new ();
        private List<JsonProcessor?> operationJsonProcessorOverrides = new ();
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
                requestOptions = SelectAndSanitize(
                    requestOptions,
                    this.defaultJsonProcessor,
                    storeSelectedProcessor: false,
                    out _,
                    out JsonProcessor? jsonProcessorOverride);
                lock (this.operationStateLock)
                {
                    this.transactionalBatch = this.transactionalBatch.CreateItem(
                        item,
                        requestOptions);
                    this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
                }

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
            requestOptions = SelectAndSanitize(
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
                        CreateProcessorPinnedRequestOptions(
                            encryptionItemRequestOptions.EncryptionOptions,
                            jsonProcessor),
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            lock (this.operationStateLock)
            {
                this.transactionalBatch = this.transactionalBatch.CreateItemStream(
                    streamPayload,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
            }

            return this;
        }

        public override TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            requestOptions = SelectAndSanitize(
                requestOptions,
                this.defaultJsonProcessor,
                storeSelectedProcessor: false,
                out _,
                out JsonProcessor? jsonProcessorOverride);
            lock (this.operationStateLock)
            {
                this.transactionalBatch = this.transactionalBatch.DeleteItem(
                    id,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
            }

            return this;
        }

        public override TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            requestOptions = SelectAndSanitize(
                requestOptions,
                this.defaultJsonProcessor,
                storeSelectedProcessor: false,
                out _,
                out JsonProcessor? jsonProcessorOverride);
            lock (this.operationStateLock)
            {
                this.transactionalBatch = this.transactionalBatch.ReadItem(
                    id,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
            }

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
                requestOptions = SelectAndSanitize(
                    requestOptions,
                    this.defaultJsonProcessor,
                    storeSelectedProcessor: false,
                    out _,
                    out JsonProcessor? jsonProcessorOverride);
                lock (this.operationStateLock)
                {
                    this.transactionalBatch = this.transactionalBatch.ReplaceItem(
                        id,
                        item,
                        requestOptions);
                    this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
                }

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
            requestOptions = SelectAndSanitize(
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
                        CreateProcessorPinnedRequestOptions(
                            encryptionItemRequestOptions.EncryptionOptions,
                            jsonProcessor),
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            lock (this.operationStateLock)
            {
                this.transactionalBatch = this.transactionalBatch.ReplaceItemStream(
                    id,
                    streamPayload,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
            }

            return this;
        }

        public override TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is not EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                requestOptions = SelectAndSanitize(
                    requestOptions,
                    this.defaultJsonProcessor,
                    storeSelectedProcessor: false,
                    out _,
                    out JsonProcessor? jsonProcessorOverride);
                lock (this.operationStateLock)
                {
                    this.transactionalBatch = this.transactionalBatch.UpsertItem(
                        item,
                        requestOptions);
                    this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
                }

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
            requestOptions = SelectAndSanitize(
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
                        CreateProcessorPinnedRequestOptions(
                            encryptionItemRequestOptions.EncryptionOptions,
                            jsonProcessor),
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            lock (this.operationStateLock)
            {
                this.transactionalBatch = this.transactionalBatch.UpsertItemStream(
                    streamPayload,
                    requestOptions);
                this.operationJsonProcessorOverrides.Add(jsonProcessorOverride);
            }

            return this;
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync"))
            {
                Task<TransactionalBatchResponse> executeTask;
                List<JsonProcessor?> operationJsonProcessorOverrides;
                lock (this.operationStateLock)
                {
                    operationJsonProcessorOverrides = this.operationJsonProcessorOverrides;
                    this.operationJsonProcessorOverrides = new List<JsonProcessor?>();
                    executeTask = this.transactionalBatch.ExecuteAsync(cancellationToken);
                }

                TransactionalBatchResponse response = await executeTask;
                return await this.DecryptTransactionalBatchResponseAsync(
                    response,
                    this.defaultJsonProcessor,
                    operationJsonProcessorOverrides,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            TransactionalBatchRequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            requestOptions = SelectAndSanitize(
                requestOptions,
                this.defaultJsonProcessor,
                storeSelectedProcessor: false,
                out JsonProcessor jsonProcessor,
                out _);
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync.WithRequestOptions"))
            {
                Task<TransactionalBatchResponse> executeTask;
                List<JsonProcessor?> operationJsonProcessorOverrides;
                lock (this.operationStateLock)
                {
                    operationJsonProcessorOverrides = this.operationJsonProcessorOverrides;
                    this.operationJsonProcessorOverrides = new List<JsonProcessor?>();
                    executeTask = this.transactionalBatch.ExecuteAsync(requestOptions, cancellationToken);
                }

                TransactionalBatchResponse response = await executeTask;
                return await this.DecryptTransactionalBatchResponseAsync(
                    response,
                    jsonProcessor,
                    operationJsonProcessorOverrides,
                    diagnosticsContext,
                    cancellationToken);
            }
        }

        private async Task<TransactionalBatchResponse> DecryptTransactionalBatchResponseAsync(
            TransactionalBatchResponse response,
            JsonProcessor batchJsonProcessor,
            IReadOnlyList<JsonProcessor?> operationJsonProcessorOverrides,
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
                int operationCount = operationJsonProcessorOverrides.Count;
                if (response.Count != operationCount)
                {
                    throw CreateResultCountMismatchException(operationCount, response.Count);
                }

                foreach (TransactionalBatchOperationResult result in response)
                {
                    if (operationIndex >= operationCount)
                    {
                        throw CreateResultCountMismatchException(operationCount, operationIndex + 1);
                    }

                    Stream resourceStream = result.ResourceStream;
                    if (response.IsSuccessStatusCode && resourceStream != null)
                    {
                        JsonProcessor jsonProcessor = operationJsonProcessorOverrides[operationIndex] ?? batchJsonProcessor;
                        Stream decryptedStream;
                        if (jsonProcessor == JsonProcessor.Newtonsoft)
                        {
                            (decryptedStream, _) = await EncryptionProcessor.DecryptAsync(
                                resourceStream,
                                this.encryptor,
                                diagnosticsContext,
                                requestOptions: null,
                                cancellationToken);
                        }
                        else
                        {
                            (decryptedStream, _) = await EncryptionProcessor.DecryptAsync(
                                resourceStream,
                                this.encryptor,
                                jsonProcessor,
                                legacyFallback: true,
                                diagnosticsContext,
                                cancellationToken);
                        }

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

                if (operationIndex != operationCount)
                {
                    throw CreateResultCountMismatchException(operationCount, operationIndex);
                }

                return decryptedResponse;
            }
            catch
            {
                try
                {
                    decryptedResponse.Dispose();
                }
                catch
                {
                    // Preserve the original decryption or response-shape failure after best-effort cleanup.
                }

                throw;
            }
        }

        private static InvalidOperationException CreateResultCountMismatchException(
            int operationCount,
            int resultCount)
        {
            return new InvalidOperationException(
                $"Transactional batch response contained {resultCount} operation results for {operationCount} operations.");
        }

        private static TRequestOptions SelectAndSanitize<TRequestOptions>(
            TRequestOptions requestOptions,
            JsonProcessor defaultJsonProcessor,
            bool storeSelectedProcessor,
            out JsonProcessor jsonProcessor,
            out JsonProcessor? jsonProcessorOverride)
            where TRequestOptions : RequestOptions
        {
            bool hasOverride = requestOptions.TryReadJsonProcessorOverride(out jsonProcessor);
            if (!hasOverride)
            {
                jsonProcessor = defaultJsonProcessor;
            }

            jsonProcessorOverride = hasOverride || storeSelectedProcessor ? jsonProcessor : null;
            if (requestOptions?.Properties == null ||
                !requestOptions.Properties.ContainsKey(JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey))
            {
                return requestOptions;
            }

            TRequestOptions sanitizedOptions = (TRequestOptions)requestOptions.ShallowCopy();
            Dictionary<string, object> properties = new ();
            foreach (KeyValuePair<string, object> property in requestOptions.Properties)
            {
                if (property.Key != JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey)
                {
                    properties[property.Key] = property.Value;
                }
            }

            sanitizedOptions.Properties = properties.Count == 0 ? null : properties;
            return sanitizedOptions;
        }

        private static EncryptionTransactionalBatchItemRequestOptions CreateProcessorPinnedRequestOptions(
            EncryptionOptions encryptionOptions,
            JsonProcessor jsonProcessor)
        {
            return new EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions = encryptionOptions,
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, jsonProcessor },
                },
            };
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
