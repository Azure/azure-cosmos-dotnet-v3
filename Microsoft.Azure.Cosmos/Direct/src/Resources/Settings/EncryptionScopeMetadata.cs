//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;

    /// <summary>
    /// Encryption scope metadata(from encryption scope resource) stored in the collection body.
    /// This contains all the required metadata from encryption scoper resource which is needed
    /// to call the azure key vault to get the unwrapped dek to encrypt/decrypt the data
    /// being stored in the collection
    /// </summary>
    internal sealed class EncryptionScopeMetadata : JsonSerializable, ICloneable
    {
        private Collection<CMKMetadataInfo> cmkMetadataList;

        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Id);
            }
            set
            {
                this.SetValue(Constants.Properties.Id, value);
            }
        }

        [JsonProperty(PropertyName = Constants.EncryptionScopeProperties.Name)]
        public string Name
        {
            get
            {
                return base.GetValue<string>(Constants.EncryptionScopeProperties.Name);
            }
            set
            {
                this.SetValue(Constants.EncryptionScopeProperties.Name, value);
            }
        }

        [JsonProperty(PropertyName = Constants.KeyVaultProperties.DataEncryptionKeyStatus)]
        public DataEncryptionKeyStatus DataEncryptionKeyStatus
        {
            get
            {
                return base.GetValue<DataEncryptionKeyStatus>(Constants.KeyVaultProperties.DataEncryptionKeyStatus);
            }
            set
            {
                this.SetValue(Constants.KeyVaultProperties.DataEncryptionKeyStatus, value);
            }
        }

        [JsonProperty(PropertyName = Constants.EncryptionScopeProperties.CMKMetadataList)]
        public Collection<CMKMetadataInfo> CMKMetadataList
        {
            get
            {
                if (this.cmkMetadataList == null)
                {
                    this.cmkMetadataList =
                        base.GetObjectCollection<CMKMetadataInfo>(
                            Constants.EncryptionScopeProperties.CMKMetadataList);

                    if (this.cmkMetadataList == null)
                    {
                        this.cmkMetadataList = new Collection<CMKMetadataInfo>();
                    }
                }

                return this.cmkMetadataList;
            }
            set
            {
                this.cmkMetadataList = value;
            }
        }

        public object Clone()
        {
            EncryptionScopeMetadata cloned = new EncryptionScopeMetadata()
            {
                Id = this.Id,
                Name = this.Name,
                DataEncryptionKeyStatus = this.DataEncryptionKeyStatus
            };

            foreach (CMKMetadataInfo item in this.CMKMetadataList)
            {
                cloned.CMKMetadataList.Add((CMKMetadataInfo)item.Clone());
            }

            return cloned;
        }

        internal override void OnSave()
        {
            base.OnSave();            

            if (this.cmkMetadataList != null)
            {
                base.SetObjectCollection(Constants.EncryptionScopeProperties.CMKMetadataList, this.cmkMetadataList);
            }
        }
    }
}
