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
                    Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        this.encryptor,
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
                List<T> decryptableContent = null;

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    decryptableContent = this.ConvertResponseToDecryptableItems<T>(
                        responseMessage.Content);

                    return (responseMessage, decryptableContent);
                }

                return (responseMessage, decryptableContent);
            }
        }

        private List<T> ConvertResponseToDecryptableItems<T>(
            Stream content)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);

            if (contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is not JArray documents)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed Response did not have an array of Documents.");
            }

            List<T> decryptableItems = new (documents.Count);

            foreach (JToken value in documents)
            {
                DecryptableItemCore item = new (
                    value,
                    this.encryptor,
                    this.cosmosSerializer);

                decryptableItems.Add((T)(object)item);
            }

            return decryptableItems;
        }
    }
}
