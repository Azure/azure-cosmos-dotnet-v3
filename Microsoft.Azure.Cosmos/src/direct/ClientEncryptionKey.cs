//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a client encryption key in the Azure Cosmos DB service.
    /// </summary>
    internal class ClientEncryptionKey: Resource
    {
        private KeyWrapMetadata keyWrapMetadata;

        public ClientEncryptionKey()
        {

        }

        [JsonProperty(PropertyName = Constants.Properties.EncryptionAlgorithm)]
        internal string EncryptionAlgorithm
        {
            get
            {
                return this.GetValue<string>(Constants.Properties.EncryptionAlgorithm);
            }
            set
            {
                base.SetValue(Constants.Properties.EncryptionAlgorithm, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.WrappedDataEncryptionKey)]
        internal byte[] WrappedDataEncryptionKey
        {
            get
            {
                return this.GetValue<byte[]>(Constants.Properties.WrappedDataEncryptionKey);
            }
            set
            {
                base.SetValue(Constants.Properties.WrappedDataEncryptionKey, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.KeyWrapMetadata)]
        internal KeyWrapMetadata KeyWrapMetadata
        {
            get
            {
                if (this.keyWrapMetadata == null)
                {
                    this.keyWrapMetadata = base.GetObject<KeyWrapMetadata>(Constants.Properties.KeyWrapMetadata) ?? new KeyWrapMetadata();
                }

                return keyWrapMetadata;
            }
            set
            {
                this.keyWrapMetadata = value;
                base.SetObject<KeyWrapMetadata>(Constants.Properties.KeyWrapMetadata, value);
            }
        }

        internal override void OnSave()
        {
            if (this.keyWrapMetadata != null)
            {
                this.keyWrapMetadata.OnSave();
                base.SetObject(Constants.Properties.KeyWrapMetadata, this.keyWrapMetadata);
            }
        }
    }
}
