//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Metadata that can be used to wrap/unwrap a Data Encryption Key using a Customer Managed Key.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public class EncryptionKeyWrapMetadata : IEquatable<EncryptionKeyWrapMetadata>
    {
        // For JSON deserialize
        private EncryptionKeyWrapMetadata()
        {
        }

        /// <summary>
        /// Creates a new instance of key wrap metadata.
        /// </summary> 
        /// <param name="type">Identifier for the key resolver.</param>
        /// <param name="name">Identifier for the customer managed key.</param>
        /// <param name="value">Path to the customer managed key.</param>
        /// <param name="algorithm">Algorithm used in wrapping and unwrapping of the data encryption key.</param>
        public EncryptionKeyWrapMetadata(string type, string name, string value, string algorithm)
        {
            this.Type = type ?? throw new ArgumentNullException(nameof(type));
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
            this.Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        }

        /// <summary>
        /// Creates a new instance of key wrap metadata based on an existing instance.
        /// </summary>
        /// <param name="source">Existing instance from which to initialize.</param>
        public EncryptionKeyWrapMetadata(EncryptionKeyWrapMetadata source)
            : this(source?.Type, source?.Name, source?.Value, source?.Algorithm)
        {
        }

        /// <summary>
        /// Serialized form of metadata.
        /// Note: This value is saved in the Cosmos DB service.
        /// Implementors of derived implementations should ensure that this does not have (private) key material or credential information.
        /// </summary>
        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; private set; }

        /// <summary>
        /// Serialized form of metadata.
        /// Note: This value is saved in the Cosmos DB service.
        /// Implementors of derived implementations should ensure that this does not have (private) key material or credential information.
        /// </summary>
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; private set; }

        /// <summary>
        /// Serialized form of metadata.
        /// Note: This value is saved in the Cosmos DB service.
        /// Implementors of derived implementations should ensure that this does not have (private) key material or credential information.
        /// </summary>
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; private set; }

        /// <summary>
        /// Serialized form of metadata.
        /// Note: This value is saved in the Cosmos DB service.
        /// Implementors of derived implementations should ensure that this does not have (private) key material or credential information.
        /// </summary>
        [JsonProperty(PropertyName = "algorithm", NullValueHandling = NullValueHandling.Ignore)]
        public string Algorithm { get; private set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            EncryptionKeyWrapMetadata metadata = obj as EncryptionKeyWrapMetadata;
            return this.Equals(metadata);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1265339359;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Type);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Name);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Value);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Algorithm);
            return hashCode;
        }

        /// <summary>
        /// Returns whether the properties of the passed in key wrap metadata matches with those in the current instance.
        /// </summary>
        /// <param name="other">Key wrap metadata to be compared with current instance.</param>
        /// <returns>
        /// True if the properties of the key wrap metadata passed in matches with those in the current instance, else false.
        /// </returns>
        public bool Equals(EncryptionKeyWrapMetadata other)
        {
            return other != null &&
                   this.Type == other.Type &&
                   this.Name == other.Name &&
                   this.Value == other.Value &&
                   this.Algorithm == other.Algorithm &&
                   this.AdditionalProperties.EqualsTo(other.AdditionalProperties);
        }
    }
}