//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    internal class EncryptionProperties
    {
        [JsonProperty(PropertyName = "_ef")]
        public int EncryptionFormatVersion { get; private set; }

        [JsonProperty(PropertyName = "_ek")]
        public string DataEncryptionKeyRid { get; private set; }

        [JsonProperty(PropertyName = "_ea")]
        public int EncryptionAlgorithmId { get; private set; }

        [JsonProperty(PropertyName = "_ed")]
        public byte[] EncryptedData { get; private set; }

        public EncryptionProperties(
            int encryptionFormatVersion,
            string dataEncryptionKeyRid,
            int encryptionAlgorithmId,
            byte[] encryptedData)
        {
            this.EncryptionFormatVersion = encryptionFormatVersion;
            this.DataEncryptionKeyRid = dataEncryptionKeyRid;
            this.EncryptionAlgorithmId = encryptionAlgorithmId;
            this.EncryptedData = encryptedData;
        }
    }
}
