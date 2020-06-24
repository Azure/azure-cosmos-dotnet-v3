//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Newtonsoft.Json;

    internal class EncryptionProperties
    {
        [JsonProperty(PropertyName = Constants.EncryptionFormatVersion)]
        public int EncryptionFormatVersion { get; }

        [JsonProperty(PropertyName = Constants.EncryptionDekId)]
        public string DataEncryptionKeyId { get; }

        [JsonProperty(PropertyName = Constants.EncryptionAlgorithm)]
        public string EncryptionAlgorithm { get; }

        [JsonProperty(PropertyName = Constants.EncryptedData)]
        public byte[] EncryptedData { get; }
        [JsonProperty(PropertyName = Constants.EncryptedPaths)]
        public string EncryptedPaths { get; }

        public EncryptionProperties(
            int encryptionFormatVersion,
            string encryptionAlgorithm,
            string dataEncryptionKeyId,
            byte[] encryptedData,
            string encryptedPaths = null)
        {
            this.EncryptionFormatVersion = encryptionFormatVersion;
            this.EncryptionAlgorithm = encryptionAlgorithm;
            this.DataEncryptionKeyId = dataEncryptionKeyId;
            this.EncryptedData = encryptedData;
            this.EncryptedPaths = encryptedPaths;
        }
    }
}
