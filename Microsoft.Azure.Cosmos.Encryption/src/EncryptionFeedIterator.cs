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
        private readonly RequestOptions requestOptions;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            EncryptionContainer encryptionContainer,
            RequestOptions requestOptions)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.requestOptions = requestOptions ?? throw new ArgumentNullException(nameof(requestOptions));
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                EncryptionSettings encryptionSettings = await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(cancellationToken);
                encryptionSettings.SetRequestHeaders(this.requestOptions);

                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                // check for Bad Request and Wrong RID intended and update the cached RID and Client Encryption Policy.
                if (responseMessage.StatusCode == HttpStatusCode.BadRequest
                    && string.Equals(responseMessage.Headers.Get(EncryptionContainer.SubStatusHeader), EncryptionContainer.IncorrectContainerRidSubStatus))
                {
                    await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(
                        cancellationToken: cancellationToken,
                        obsoleteEncryptionSettings: encryptionSettings);

                    throw new CosmosException(
                        "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + responseMessage.ErrorMessage,
                        responseMessage.StatusCode,
                        int.Parse(EncryptionContainer.IncorrectContainerRidSubStatus),
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
            if (!encryptionSettings.PropertiesToEncrypt.Any())
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
