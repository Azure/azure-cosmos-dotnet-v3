//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
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
        /// Decrypt an input of encrypted stream data.
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
        /// Decrypt an input of encrypted JObject.
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

        public void ValidateInputForEncrypt(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            if (encryptionOptions == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions));
            }

            if (string.IsNullOrWhiteSpace(encryptionOptions.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.DataEncryptionKeyId));
            }

            if (string.IsNullOrWhiteSpace(encryptionOptions.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.EncryptionAlgorithm));
            }

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
            }
        }
		
        public JObject RetrieveItem(
            Stream input)

        {
            if (input == null)
            {
                return (input, null);
            }

            Debug.Assert(input.CanSeek);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
            {
                itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
            }

            return itemJObj;
        }

        public JObject RetrieveEncryptionProperties(
            JObject item)
        {
            JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            return encryptionPropertiesJObj;
        }
    }
}
