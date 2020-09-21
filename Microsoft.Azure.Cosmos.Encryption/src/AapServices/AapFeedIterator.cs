//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class AapFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly Action<DecryptionResult> decryptionResultHandler;

        public AapFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            Action<DecryptionResult> decryptionResultHandler = null)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.decryptionResultHandler = decryptionResultHandler;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Stream decryptedContent = await this.DecryptFeedResponseAsync(
                        responseMessage.Content,
                        diagnosticsContext,
                        cancellationToken);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        private async Task<MemoryStream> DecryptFeedResponseAsync(
            Stream input,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            MemoryStream outputStream = new MemoryStream();
            EncryptionProperties encryptionProperties = null;

            using (JsonDocument response = JsonDocument.Parse(input))
            using (Utf8JsonWriter writer = new Utf8JsonWriter(outputStream))
            {
                JsonElement root = response.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Invalid document to decrypt", nameof(input));
                }

                writer.WriteStartObject();

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Name == Constants.DocumentsResourcePropertyName)
                    {
                        if (property.Value.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException($"Unexpected type {property.Value.ValueKind} for {Constants.DocumentsResourcePropertyName}");
                        }
                        else
                        {
                            writer.WritePropertyName(Constants.DocumentsResourcePropertyName);
                            writer.WriteStartArray();

                            foreach (JsonElement doc in property.Value.EnumerateArray())
                            {
                                if (doc.ValueKind == JsonValueKind.Object)
                                {
                                    try
                                    {
                                        JObject document = JObject.Parse(doc.GetRawText());
                                        if (document.TryGetValue(Constants.EncryptedInfo, out JToken encryptedInfo))
                                        {
                                             encryptionProperties = JsonConvert.DeserializeObject<EncryptionProperties>(encryptedInfo.ToString());
                                        }

                                        // Docs which were not encrypted will have no encryption properties,
                                        // we just build the document in the outstream
                                        await AapEncryptionProcessor.DecryptAndWriteAsync(
                                            doc,
                                            this.encryptor,
                                            writer,
                                            encryptionProperties: encryptionProperties,
                                            cancellationToken: cancellationToken);

                                        // reset for next iteration,the iterator can have documents  with
                                        // both encrypted and plain documents with no encryption info attached.
                                        encryptionProperties = null;
                                    }
                                    catch (Exception exception)
                                    {
                                        if (this.decryptionResultHandler == null)
                                        {
                                            throw;
                                        }

                                        // send back the document info.
                                        JObject document = JObject.Parse(doc.GetRawText());
                                        MemoryStream memoryStream = EncryptionProcessor.BaseSerializer.ToStream(document);
                                        Debug.Assert(memoryStream != null);
                                        bool wasBufferReturned = memoryStream.TryGetBuffer(out ArraySegment<byte> encryptedStream);
                                        Debug.Assert(wasBufferReturned);

                                        this.decryptionResultHandler(
                                            DecryptionResult.CreateFailure(
                                                encryptedStream,
                                                exception));
                                    }
                                }
                            }

                            writer.WriteEndArray();
                        }
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            return outputStream;
        }
    }
}
