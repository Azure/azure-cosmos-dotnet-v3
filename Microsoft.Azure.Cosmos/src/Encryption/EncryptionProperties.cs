//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal class EncryptionProperties
    {
        [JsonProperty(PropertyName = Constants.Properties.EncryptionFormatVersion)]
        public int EncryptionFormatVersion { get; }

        [JsonProperty(PropertyName = "_en")]
        public string DataEncryptionKeyId { get; }

        [JsonProperty(PropertyName = "_ea")]
        public CosmosEncryptionAlgorithm EncryptionAlgorithmId { get;  }

        [JsonProperty(PropertyName = Constants.Properties.EncryptedData)]
        public byte[] EncryptedData { get; }

        public EncryptionProperties(
            int encryptionFormatVersion,
            CosmosEncryptionAlgorithm encryptionAlgorithmId,
            string dataEncryptionKeyId,
            byte[] encryptedData)
        {
            this.EncryptionFormatVersion = encryptionFormatVersion;
            this.EncryptionAlgorithmId = encryptionAlgorithmId;
            this.DataEncryptionKeyId = dataEncryptionKeyId;
            this.EncryptedData = encryptedData;
        }
    }
}
