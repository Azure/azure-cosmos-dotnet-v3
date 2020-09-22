// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class DecryptableItemCore : DecryptableItem
    {
        /// <summary>
        /// The encrypted content which is yet to be decrypted.
        /// </summary>
        private readonly JToken decryptableContent;
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;

        public DecryptableItemCore(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.decryptableContent = decryptableContent ?? throw new ArgumentNullException(nameof(decryptableContent));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
        }

        /// <summary>
        /// Decrypts and deserializes the content.
        /// </summary>
        /// <typeparam name="T">The type of item to be returned.</typeparam>
        /// <returns>The requested item and the decryption information.</returns>
        public override async Task<(T, DecryptionInfo)> GetItemAsync<T>()
        {
            (Stream decryptedStream, DecryptionInfo decryptionInfo) = await this.GetItemAsStreamAsync();
            return (this.cosmosSerializer.FromStream<T>(decryptedStream), decryptionInfo);
        }

        /// <summary>
        /// Decrypts the content and outputs stream.
        /// </summary>
        /// <returns>Decrypted stream response and the decryption information.</returns>
        public override async Task<(Stream, DecryptionInfo)> GetItemAsStreamAsync()
        {
            if (!(this.decryptableContent is JObject document))
            {
                return (EncryptionProcessor.BaseSerializer.ToStream(this.decryptableContent), null);
            }

            (JObject decryptedItem, DecryptionInfo decryptionInfo) = await EncryptionProcessor.DecryptAsync(
                document,
                this.encryptor,
                new CosmosDiagnosticsContext(),
                cancellationToken: default);

            return (EncryptionProcessor.BaseSerializer.ToStream(decryptedItem), decryptionInfo);
        }
    }
}
