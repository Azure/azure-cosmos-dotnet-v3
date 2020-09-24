//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Abstraction for performing client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    internal abstract class EncryptionProcessor
    {
        public static readonly CosmosJsonDotNetSerializer BaseSerializer =
            new CosmosJsonDotNetSerializer(
                new JsonSerializerSettings()
                {
                    DateParseHandling = DateParseHandling.None,
                });

        /// <summary>
        /// Encrypt an input of stream data.
        /// </summary>
        /// <param name="input"> Input Stream to be encrypted </param>
        /// <param name="encryptor"> Encryptor </param>
        /// <param name="encryptionOptions"> Encryption Options </param>
        /// <param name="diagnosticsContext"> Diagnostics Context</param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> Decrypted Stream </returns>
        public abstract Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken);

        /// <summary>
        /// Encrypt an input of encrypted stream data.
        /// </summary>
        /// <param name="input"> Input Stream to be decrypted </param>
        /// <param name="encryptor"> Encryptor </param>
        /// <param name="diagnosticsContext"> Diagnostics Context </param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> Decrypted Stream </returns>
        public abstract Task<Stream> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken);

        /// <summary>
        /// Encrypt an input of encrypted JObject.
        /// </summary>
        /// <param name="document"> Input JObject to be decrypted </param>
        /// <param name="encryptor"> Encryptor </param>
        /// <param name="diagnosticsContext"> Diagnostics Context </param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> Decrypted JObject </returns>
        public abstract Task<JObject> DecryptAsync(
           JObject document,
           Encryptor encryptor,
           CosmosDiagnosticsContext diagnosticsContext,
           CancellationToken cancellationToken);
    }
}
