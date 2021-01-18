//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class MdeEncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly MdeEncryptionProcessor mdeEncryptionProcessor;

        public MdeEncryptionFeedIterator(
            FeedIterator feedIterator,
            MdeEncryptionProcessor mdeEncryptionProcessor)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.mdeEncryptionProcessor = mdeEncryptionProcessor ?? throw new ArgumentNullException(nameof(mdeEncryptionProcessor));
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

        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = MdeEncryptionProcessor.BaseSerializer.FromStream<JObject>(content);
            JArray results = new JArray();

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents. ");
            }

            foreach (JToken value in documents)
            {
                if (value is not JObject document)
                {
                    results.Add(value);
                    continue;
                }

                JObject decryptedDocument = await this.mdeEncryptionProcessor.DecryptAsync(
                    document,
                    diagnosticsContext,
                    cancellationToken);

                results.Add(decryptedDocument);
            }

            JObject decryptedResponse = new JObject();
            foreach (JProperty property in contentJObj.Properties())
            {
                if (property.Name.Equals(Constants.DocumentsResourcePropertyName))
                {
                    decryptedResponse.Add(property.Name, (JToken)results);
                }
                else
                {
                    decryptedResponse.Add(property.Name, property.Value);
                }
            }

            return MdeEncryptionProcessor.BaseSerializer.ToStream(decryptedResponse);
        }
    }
}
