//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// Path that needs encryption and the associated settings within <see cref="ClientEncryptionPolicy"/>.
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif
        sealed class ClientEncryptionIncludedPath
    {
        /// <summary>
        /// Gets or sets the path to be encrypted. Must be a top level path, eg. /salary
        /// </summary>
        [JsonProperty(PropertyName = "path")]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the Client Encryption Key to be used to encrypt the path.
        /// </summary>
        [JsonProperty(PropertyName = "clientEncryptionKeyId")]
        public string ClientEncryptionKeyId { get; set; }

        /// <summary>
        /// Gets or sets the type of encryption to be performed. Eg - Deterministic, Randomized
        /// </summary>
        [JsonProperty(PropertyName = "encryptionType")]
        public string EncryptionType { get; set; }

        /// <summary>
        /// Gets or sets the encryption algorithm which will be used. Eg - AEAes256CbcHmacSha256
        /// </summary>
        [JsonProperty(PropertyName = "encryptionAlgorithm")]
        public string EncryptionAlgorithm { get; set; }
    }
}
