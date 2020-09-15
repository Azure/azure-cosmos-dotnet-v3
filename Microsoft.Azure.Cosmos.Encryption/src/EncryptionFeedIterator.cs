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
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.cosmosSerializer = cosmosSerializer;
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
                    Stream decryptedContent = await this.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        diagnosticsContext,
                        cancellationToken);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        public async Task<(ResponseMessage, List<T>)> ReadNextWithoutDecryptionAsync<T>(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNextWithoutDecryption"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    List<T> decryptableContent = this.ConvertResponseToDecryptableItems<T>(
                        responseMessage.Content);

                    return (responseMessage, decryptableContent);
                }

                return (responseMessage, null);
            }
        }

        private List<T> ConvertResponseToDecryptableItems<T>(
            Stream content)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed Response did not have an array of Documents.");
            }

            List<T> decryptableItems = new List<T>();

            foreach (JToken value in documents)
            {
                DecryptableItemCore item = new DecryptableItemCore(
                    value,
                    this.encryptor,
                    this.cosmosSerializer);

                decryptableItems.Add((T)(object)item);
            }

            return decryptableItems;
        }

        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);
            JArray result = new JArray();

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed response Body Contract was violated. Feed response did not have an array of Documents");
            }

            foreach (JToken value in documents)
            {
                if (!(value is JObject document))
                {
                    result.Add(value);
                    continue;
                }

                (JObject decryptedDocument, DecryptionInfo _) = await EncryptionProcessor.DecryptAsync(
                    document,
                    this.encryptor,
                    diagnosticsContext,
                    cancellationToken);

                result.Add(decryptedDocument);
            }

            JObject decryptedResponse = new JObject();
            foreach (JProperty property in contentJObj.Properties())
            {
                if (property.Name.Equals(Constants.DocumentsResourcePropertyName))
                {
                    decryptedResponse.Add(property.Name, (JToken)result);
                }
                else
                {
                    decryptedResponse.Add(property.Name, property.Value);
                }
            }

            return EncryptionProcessor.BaseSerializer.ToStream(decryptedResponse);
        }
    }
}
