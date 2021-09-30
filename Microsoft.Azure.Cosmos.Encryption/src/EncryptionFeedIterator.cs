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
            await this.encryptionContainer.ThrowIfRequestNeedsARetryPostPolicyRefreshAsync(responseMessage, encryptionSettings, cancellationToken);

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
