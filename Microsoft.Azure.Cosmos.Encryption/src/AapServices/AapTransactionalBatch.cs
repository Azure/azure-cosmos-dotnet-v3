//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "To be fixed, tracked in issue #1575")]
    internal sealed class AapTransactionalBatch : TransactionalBatch
    {
        private readonly CosmosSerializer cosmosSerializer;
        private readonly Encryptor encryptor;
        private TransactionalBatch transactionalBatch;

        public AapTransactionalBatch(
            TransactionalBatch transactionalBatch,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            Debug.Assert(transactionalBatch != null);
            Debug.Assert(cosmosSerializer != null);

            this.transactionalBatch = transactionalBatch;
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer;
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
                if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                    encryptionItemRequestOptions.EncryptionOptions != null)
                {
                    // Per Item Encryption Policy
                    streamPayload = AapEncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionOptions: encryptionItemRequestOptions.EncryptionOptions,
                        cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
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
                if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                    encryptionItemRequestOptions.EncryptionOptions != null)
                {
                    streamPayload = AapEncryptionProcessor.EncryptAsync(
                    streamPayload,
                    this.encryptor,
                    encryptionOptions: encryptionItemRequestOptions.EncryptionOptions,
                    cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
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
                if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                   encryptionItemRequestOptions.EncryptionOptions != null)
                {
                    streamPayload = AapEncryptionProcessor.EncryptAsync(
                    streamPayload,
                    this.encryptor,
                    encryptionOptions: encryptionItemRequestOptions.EncryptionOptions,
                    cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
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

                if (response.IsSuccessStatusCode)
                {
                    for (int index = 0; index < response.Count; index++)
                    {
                        TransactionalBatchOperationResult result = response[index];

                        if (result.ResourceStream != null)
                        {
                            JObject itemJObj;
                            Stream input = result.ResourceStream;
                            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                            using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                            {
                                itemJObj = Newtonsoft.Json.JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
                            }

                            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.EncryptedInfo);
                            JObject encryptionPropertiesJObj = null;
                            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
                            {
                                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
                            }

                            EncryptionProperties encryptionProperties = null;
                            if (encryptionPropertiesJObj != null)
                            {
                                encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
                            }

                            input.Seek(0, SeekOrigin.Begin);
                            MemoryStream outputStream = new MemoryStream();
                            using (Utf8JsonWriter writer = new Utf8JsonWriter(outputStream))
                            {
                                await AapEncryptionProcessor.DecryptAndWriteAsync(
                                    JsonDocument.Parse(result.ResourceStream).RootElement,
                                    this.encryptor,
                                    writer,
                                    encryptionProperties: encryptionProperties,
                                    cancellationToken: cancellationToken);
                            }

                            outputStream.Seek(0, SeekOrigin.Begin);
                            result.ResourceStream = outputStream;
                        }
                    }
                }

                return response;
            }
        }
    }
}
