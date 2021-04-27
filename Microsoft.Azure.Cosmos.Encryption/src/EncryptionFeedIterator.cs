//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly EncryptionContainer encryptionContainer;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            EncryptionContainer encryptionContainer)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                EncryptionSettings encryptionSettings = await this.encryptionContainer.GetorUpdateEncryptionSettingsFromCacheAsync(cancellationToken);

                // check for Bad Request and Wrong RID intended and update the cached RID and Client Encryption Policy.
                if (responseMessage.StatusCode == HttpStatusCode.BadRequest
                    && string.Equals(responseMessage.Headers.Get("x-ms-substatus"), "1024"))
                {
                    await this.encryptionContainer.InitEncryptionContainerCacheIfNotInitAsync(
                        cancellationToken: cancellationToken,
                        obsoleteEncryptionSettings: encryptionSettings,
                        shouldForceRefresh: true);

                    throw new CosmosException(
                        "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + responseMessage.ErrorMessage,
                        responseMessage.StatusCode,
                        1024,
                        responseMessage.Headers.ActivityId,
                        responseMessage.Headers.RequestCharge);
                }

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Stream decryptedContent = await this.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        encryptionSettings,
                        diagnosticsContext,
                        cancellationToken);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            EncryptionSettings encryptionSettings,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (!encryptionSettings.GetClientEncryptionPolicyPaths.Any())
            {
                return content;
            }

            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);
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

                JObject decryptedDocument = await EncryptionProcessor.DecryptAsync(
                    document,
                    encryptionSettings,
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

            return EncryptionProcessor.BaseSerializer.ToStream(decryptedResponse);
        }
    }
}
