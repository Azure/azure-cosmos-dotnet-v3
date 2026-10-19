//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines the CMK metadata(from the encryption scope resource) stored in the collection body.
    /// This contains the azure key vault metadata and the metadata of the assigned identity
    /// which will be used to access the key vault to get the data encryption key which will be
    /// used to encrypt/decrypt the documents that would be stored in the collection.
    /// </summary>
    internal sealed class CMKMetadataInfo : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Key vault uri for the customer managed key.
        /// </summary>
        [JsonProperty(PropertyName = Constants.KeyVaultProperties.KeyVaultKeyUri)]
        public string KeyVaultKeyUri
        {
            get
            {
                return base.GetValue<string>(Constants.KeyVaultProperties.KeyVaultKeyUri);
            }
            set
            {
                base.SetValue(Constants.KeyVaultProperties.KeyVaultKeyUri, value);
            }
        }

        /// <summary>
        /// Reference of the identity to be used to access the key vault.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DefaultIdentity)]
        public string DefaultIdentity
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.DefaultIdentity);
            }
            set
            {
                base.SetValue(Constants.Properties.DefaultIdentity, value);
            }
        }

        /// <summary>
        /// Reference for the client id of the default identity.
        /// </summary>
        [JsonProperty(PropertyName = Constants.ManagedServiceIdentityProperties.MsiClientId)]
        public string MsiClientId
        {
            get
            {
                return base.GetValue<string>(Constants.ManagedServiceIdentityProperties.MsiClientId);
            }
            set
            {
                base.SetValue(Constants.ManagedServiceIdentityProperties.MsiClientId, value);
            }
        }

        /// <summary>
        /// Reference for the client secret encrypted of the default identity.
        /// </summary>
        [JsonProperty(PropertyName = Constants.ManagedServiceIdentityProperties.MsiClientSecretEncrypted)]
        public string MsiClientSecretEncrypted
        {
            get
            {
                return base.GetValue<string>(Constants.ManagedServiceIdentityProperties.MsiClientSecretEncrypted);
            }
            set
            {
                base.SetValue(Constants.ManagedServiceIdentityProperties.MsiClientSecretEncrypted, value);
            }
        }

        /// <summary>
        /// Dek encrypted with kek provided from the customer.
        /// </summary>
        [JsonProperty(PropertyName = Constants.KeyVaultProperties.WrappedDek)]
        public string WrappedDek
        {
            get
            {
                return base.GetValue<string>(Constants.KeyVaultProperties.WrappedDek);
            }
            set
            {
                base.SetValue(Constants.KeyVaultProperties.WrappedDek, value);
            }
        }

        public object Clone()
        {
            CMKMetadataInfo cloned = new CMKMetadataInfo()
            {
                KeyVaultKeyUri = this.KeyVaultKeyUri,
                DefaultIdentity = this.DefaultIdentity,
                MsiClientId = this.MsiClientId,
                MsiClientSecretEncrypted = this.MsiClientSecretEncrypted,
                WrappedDek = this.WrappedDek,
            };

            return cloned;
        }
    }
}
