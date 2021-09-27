//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

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
            EncryptionSettings encryptionSettings = await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
            encryptionSettings.SetRequestHeaders(this.requestOptions);

            ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

            // check for Bad Request and Wrong RID intended and update the cached RID and Client Encryption Policy.
            if (responseMessage.StatusCode == HttpStatusCode.BadRequest
                && string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
            {
                await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(
                    obsoleteEncryptionSettings: encryptionSettings,
                    cancellationToken: cancellationToken);

                    throw new CosmosException(
                        "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Retrying can possibly fix the issue. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + responseMessage.ErrorMessage,
                        responseMessage.StatusCode,
                        int.Parse(Constants.IncorrectContainerRidSubStatus),
                        responseMessage.Headers.ActivityId,
                        responseMessage.Headers.RequestCharge);
                }

            if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
            {
                EncryptionDiagnosticsContext decryptDiagnostics = new EncryptionDiagnosticsContext();

                Stream decryptedContent = await EncryptionProcessor.DeserializeAndDecryptResponseAsync(
                    responseMessage.Content,
                    encryptionSettings,
                    decryptDiagnostics,
                    cancellationToken);

                decryptDiagnostics.AddEncryptionDiagnosticsToResponseMessage(responseMessage);

                return new DecryptedResponseMessage(responseMessage, decryptedContent);
            }

            return responseMessage;
        }
    }
}
