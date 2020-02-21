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

        [JsonProperty(PropertyName = Constants.Properties.DataEncryptionKeyRid)]
        public string DataEncryptionKeyRid { get; }

        [JsonProperty(PropertyName = Constants.Properties.EncryptedData)]
        public byte[] EncryptedData { get; }

        public EncryptionProperties(
            int encryptionFormatVersion,
            string dataEncryptionKeyRid,
            byte[] encryptedData)
        {
            this.EncryptionFormatVersion = encryptionFormatVersion;
            this.DataEncryptionKeyRid = dataEncryptionKeyRid;
            this.EncryptedData = encryptedData;
        }
    }
}
