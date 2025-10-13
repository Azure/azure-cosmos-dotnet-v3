//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly JsonProcessor jsonProcessor;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            JsonProcessor jsonProcessor)
        {
            this.feedIterator = feedIterator ?? throw new System.ArgumentNullException(nameof(feedIterator));
            this.encryptor = encryptor ?? throw new System.ArgumentNullException(nameof(encryptor));
            this.jsonProcessor = jsonProcessor;
        }

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            RequestOptions requestOptions)
            : this(feedIterator, encryptor, requestOptions.GetJsonProcessor())
        {
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
                        this.jsonProcessor,
                        cancellationToken);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        internal Task<ResponseMessage> ReadNextRawResponseAsync(CancellationToken cancellationToken = default)
        {
            return this.feedIterator.ReadNextAsync(cancellationToken);
        }
    }
}
