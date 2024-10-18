// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Input type should implement this abstract class for lazy decryption and to retrieve the details in the write path.
    /// </summary>
    public abstract class EncryptableItem : IDisposable
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
        [Obsolete("Use overload with outputStream")]
        protected internal abstract Stream ToStream(CosmosSerializer serializer);

        /// <summary>
        /// Gets the input payload in stream format.
        /// </summary>
        /// <param name="serializer">Cosmos Serializer</param>
        /// <param name="outputStream">Output stream</param>
        /// <param name="cancellationToken">CancellationToken</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected internal abstract Task ToStreamAsync(CosmosSerializer serializer, Stream outputStream, CancellationToken cancellationToken);

        /// <summary>
        /// Populates the DecryptableItem that can be used getting the decryption result.
        /// </summary>
        /// <param name="decryptableContent">The encrypted content which is yet to be decrypted.</param>
        /// <param name="encryptor">Encryptor instance which will be used for decryption.</param>
        /// <param name="cosmosSerializer">Serializer instance which will be used for deserializing the content after decryption.</param>
        [Obsolete("Use overload with decryptableStream")]
        protected internal abstract void SetDecryptableItem(
            JToken decryptableContent,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer);

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
        /// <summary>
        /// Populates the DecryptableItem that can be used getting the decryption result.
        /// </summary>
        /// <param name="decryptableStream">The encrypted content stream which is yet to be decrypted.</param>
        /// <param name="encryptor">Encryptor instance which will be used for decryption.</param>
        /// <param name="jsonProcessor">Json processor for decryption.</param>
        /// <param name="cosmosSerializer">Serializer instance which will be used for deserializing the content after decryption.</param>
        /// <param name="streamManager">Stream manager providing output streams.</param>
        protected internal abstract void SetDecryptableStream(
            Stream decryptableStream,
            Encryptor encryptor,
            JsonProcessor jsonProcessor,
            CosmosSerializer cosmosSerializer,
            StreamManager streamManager);
#endif

        /// <summary>
        /// Release unmananaged resources
        /// </summary>
        public abstract void Dispose();
    }
}
