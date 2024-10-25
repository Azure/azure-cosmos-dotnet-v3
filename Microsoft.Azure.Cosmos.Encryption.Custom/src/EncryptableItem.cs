// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Input type should implement this abstract class for lazy decryption and to retrieve the details in the write path.
    /// </summary>
    public abstract class EncryptableItem
    {
        /// <summary>
        /// Gets DecryptableItem
        /// </summary>
        public abstract DecryptableItem DecryptableItem { get; }

        /// <summary>
        /// Gets the input payload in stream format.
        /// </summary>
        /// <param name="serializer">Cosmos Serializer</param>
        /// <returns>Input payload in stream format</returns>
        protected internal abstract Stream ToStream(CosmosSerializer serializer);

        /// <summary>
        /// Populates the DecryptableItem that can be used getting the decryption result.
        /// </summary>
        /// <param name="decryptableContent">The encrypted content which is yet to be decrypted.</param>
        /// <param name="encryptor">Encryptor instance which will be used for decryption.</param>
        /// <param name="cosmosSerializer">Serializer instance which will be used for deserializing the content after decryption.</param>
        protected internal abstract void SetDecryptableItem(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer);
    }
}
