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

    internal sealed class DataEncryptionKeyFeedIterator<T> : FeedIterator<T>
    {
        private readonly FeedIterator feedIterator;
        private readonly CosmosResponseFactory responseFactory;

        public DataEncryptionKeyFeedIterator(
            DataEncryptionKeyFeedIterator feedIterator,
            CosmosResponseFactory responseFactory)
        {
            if (!(feedIterator is DataEncryptionKeyFeedIterator))
            {
                throw new ArgumentOutOfRangeException($"{nameof(feedIterator)} must be of type {nameof(DataEncryptionKeyFeedIterator)}.");
            }

            this.feedIterator = feedIterator;
            this.responseFactory = responseFactory;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage;

            if (typeof(T) == typeof(DataEncryptionKeyProperties))
            {
                IReadOnlyCollection<T> resource;
                (responseMessage, resource) = await this.ReadNextUsingCosmosBaseSerializerAsync(cancellationToken);

                return DecryptableFeedResponse<T>.CreateResponse(
                    responseMessage,
                    resource);
            }
            else
            {
                responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
            }

            return this.responseFactory.CreateItemFeedResponse<T>(responseMessage);
        }

        public async Task<(ResponseMessage, List<T>)> ReadNextUsingCosmosBaseSerializerAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNextWithoutDecryption"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
                List<T> dataEncryptionKeyPropertiesList = null;

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    dataEncryptionKeyPropertiesList = this.ConvertResponseToDataEncryptionKeyPropertiesList(
                        responseMessage.Content);

                    return (responseMessage, dataEncryptionKeyPropertiesList);
                }

                return (responseMessage, dataEncryptionKeyPropertiesList);
            }
        }

        private List<T> ConvertResponseToDataEncryptionKeyPropertiesList(
            Stream content)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed Response did not have an array of Documents.");
            }

            List<T> dataEncryptionKeyPropertiesList = new List<T>(documents.Count);

            foreach (JToken value in documents)
            {
                dataEncryptionKeyPropertiesList.Add(value.ToObject<T>());
            }

            return dataEncryptionKeyPropertiesList;
        }
    }
}
