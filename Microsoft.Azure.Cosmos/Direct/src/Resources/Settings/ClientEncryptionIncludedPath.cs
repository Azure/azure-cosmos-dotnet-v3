//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary>
    /// Path that needs encryption and the associated settings within <see cref="ClientEncryptionPolicy"/>.
    /// </summary>
    internal sealed class ClientEncryptionIncludedPath : JsonSerializable
    {
        /// <summary>
        /// Gets or sets the path to be encrypted. Must be a top level path, eg. /salary
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path 
        {
            get { return this.GetValue<string>(Constants.Properties.Path); }
            set { this.SetValue(Constants.Properties.Path, value); }
        }

        /// <summary>
        /// Gets or sets the identifier of the Client Encryption Key to be used to encrypt the path.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.ClientEncryptionKeyId)]
        public string ClientEncryptionKeyId
        {
            get { return this.GetValue<string>(Constants.Properties.ClientEncryptionKeyId); }
            set { this.SetValue(Constants.Properties.ClientEncryptionKeyId, value); }
        }

        /// <summary>
        /// Gets or sets the type of encryption to be performed. Eg - Deterministic, Randomized
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.EncryptionType)]
        public string EncryptionType
        {
            get { return this.GetValue<string>(Constants.Properties.EncryptionType); }
            set { this.SetValue(Constants.Properties.EncryptionType, value); }
        }

        /// <summary>
        /// Gets or sets the encryption algorithm which will be used. Eg - AEAD_AES_256_CBC_HMAC_SHA256
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.EncryptionAlgorithm)]
        public string EncryptionAlgorithm
        {
            get { return this.GetValue<string>(Constants.Properties.EncryptionAlgorithm); }
            set { this.SetValue(Constants.Properties.EncryptionAlgorithm, value); }
        }
    }
}
