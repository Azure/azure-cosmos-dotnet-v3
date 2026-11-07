// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
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

        public override async Task<(T, DecryptionContext)> GetItemAsync<T>()
        {
            if (this.decryptableContent is not JObject document)
            {
                return (this.cosmosSerializer.FromStream<T>(EncryptionProcessor.BaseSerializer.ToStream(this.decryptableContent)), null);
            }

            try
            {
                // Decryption mutates its input. Keep the original ciphertext for diagnostics and subsequent reads.
                (JObject decryptedItem, DecryptionContext decryptionContext) = await EncryptionProcessor.DecryptAsync(
                    (JObject)document.DeepClone(),
                    this.encryptor,
                    new CosmosDiagnosticsContext(),
                    cancellationToken: default);

                return (this.cosmosSerializer.FromStream<T>(EncryptionProcessor.BaseSerializer.ToStream(decryptedItem)), decryptionContext);
            }
            catch (Exception exception)
            {
                string dataEncryptionKeyId = null;
                string encryptedContent = string.Empty;
                if (document.TryGetValue(Constants.EncryptedInfo, out JToken encryptedInfo) &&
                    encryptedInfo is JObject encryptedInfoObject)
                {
                    dataEncryptionKeyId = (string)encryptedInfoObject.GetValue(Constants.EncryptionDekId);
                    if (encryptedInfoObject.TryGetValue(Constants.EncryptionAlgorithm, out JToken algorithm) &&
                        algorithm.Type != JTokenType.Null)
                    {
                        encryptedContent = document.ToString();
                    }
                }

                throw new EncryptionException(
                    dataEncryptionKeyId,
                    encryptedContent,
                    exception);
            }
        }
    }
}
