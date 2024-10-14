// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Text.Json.Serialization;

    internal class EncryptionPropertiesWrapper
    {
        [JsonPropertyName(Constants.EncryptedInfo)]
        public EncryptionProperties EncryptionProperties { get; }

        public EncryptionPropertiesWrapper(EncryptionProperties encryptionProperties)
        {
            this.EncryptionProperties = encryptionProperties;
        }
    }
}
