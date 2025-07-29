//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Path that needs encryption and the associated settings within <see cref="ClientEncryptionPolicy"/>.
    /// </summary>
    public sealed class ClientEncryptionIncludedPath
    {
        /// <summary>
        /// Gets or sets the path to be encrypted. Must be a top level path, eg. /salary
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the Client Encryption Key to be used to encrypt the path.
        /// </summary>
        [JsonPropertyName("clientEncryptionKeyId")]
        public string ClientEncryptionKeyId { get; set; }

        /// <summary>
        /// Gets or sets the type of encryption to be performed. Eg - Deterministic, Randomized
        /// </summary>
        [JsonPropertyName("encryptionType")]
        public string EncryptionType { get; set; }

        /// <summary>
        /// Gets or sets the encryption algorithm which will be used. Eg - AEAD_AES_256_CBC_HMAC_SHA256
        /// </summary>
        [JsonPropertyName("encryptionAlgorithm")]
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}
