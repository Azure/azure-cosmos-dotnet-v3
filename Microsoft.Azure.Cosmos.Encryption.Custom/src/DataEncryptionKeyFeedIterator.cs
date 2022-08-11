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

    internal sealed class DataEncryptionKeyFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;

        public DataEncryptionKeyFeedIterator(
            FeedIterator feedIterator)
        {
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                return await this.feedIterator.ReadNextAsync(cancellationToken);
            }
        }

        public async Task<(ResponseMessage, List<T>)> ReadNextUsingCosmosBaseSerializerAsync<T>(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNextWithoutDecryption"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
                List<T> decryptableContent = null;

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    decryptableContent = this.ConvertResponseToDataEncryptionItems<T>(
                        responseMessage.Content);

                    return (responseMessage, decryptableContent);
                }

                return (responseMessage, decryptableContent);
            }
        }

        private List<T> ConvertResponseToDataEncryptionItems<T>(
            Stream content)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed Response did not have an array of Documents.");
            }

            List<T> dataEncryptionKeyItems = new List<T>(documents.Count);

            foreach (JToken value in documents)
            {
                dataEncryptionKeyItems.Add(value.ToObject<T>());
            }

            return dataEncryptionKeyItems;
        }
    }
}
