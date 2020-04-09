//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary> 
    /// Details of an encryption key for use with the Azure Cosmos DB service.
    /// </summary>
    public class DataEncryptionKeyProperties : IEquatable<DataEncryptionKeyProperties>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DataEncryptionKeyProperties"/>.
        /// </summary>
        /// <param name="id">Unique identifier for the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Encryption algorithm that will be used along with this data encryption key to encrypt/decrypt data.</param>
        /// <param name="wrappedDataEncryptionKey">Wrapped (encrypted) form of the data encryption key.</param>
        /// <param name="encryptionKeyWrapMetadata">Metadata used by the configured key wrapping provider in order to unwrap the key.</param>
        public DataEncryptionKeyProperties(
            string id,
            string encryptionAlgorithm,
            byte[] wrappedDataEncryptionKey,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            DateTime createdTime)
        {
            this.Id = !string.IsNullOrEmpty(id) ? id : throw new ArgumentNullException(nameof(id));
            this.EncryptionAlgorithm = encryptionAlgorithm;
            this.WrappedDataEncryptionKey = wrappedDataEncryptionKey ?? throw new ArgumentNullException(nameof(wrappedDataEncryptionKey));
            this.EncryptionKeyWrapMetadata = encryptionKeyWrapMetadata ?? throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            this.CreatedTime = createdTime;
        }

        /// <summary>
        /// For mocking.
        /// </summary>
        protected DataEncryptionKeyProperties()
        {
        }

        internal DataEncryptionKeyProperties(DataEncryptionKeyProperties source)
        {
            this.CreatedTime = source.CreatedTime;
            this.ETag = source.ETag;
            this.Id = source.Id;
            this.EncryptionAlgorithm = source.EncryptionAlgorithm;
            this.EncryptionKeyWrapMetadata = new EncryptionKeyWrapMetadata(source.EncryptionKeyWrapMetadata);
            this.LastModified = source.LastModified;
            this.ResourceId = source.ResourceId;
            this.SelfLink = source.SelfLink;
            if (source.WrappedDataEncryptionKey != null)
            {
                this.WrappedDataEncryptionKey = new byte[source.WrappedDataEncryptionKey.Length];
                source.WrappedDataEncryptionKey.CopyTo(this.WrappedDataEncryptionKey, index: 0);
            }
        }

        /// <summary>
        /// The identifier of the resource.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; internal set; }

        /// <summary>
        /// Encryption algorithm that will be used along with this data encryption key to encrypt/decrypt data.
        /// </summary>
        [JsonProperty(PropertyName = "encryptionAlgorithm", NullValueHandling = NullValueHandling.Ignore)]
        public string EncryptionAlgorithm { get; internal set; }

        /// <summary>
        /// Wrapped form of the data encryption key.
        /// </summary>
        [JsonProperty(PropertyName = "wrappedDataEncryptionKey", NullValueHandling = NullValueHandling.Ignore)]
        public byte[] WrappedDataEncryptionKey { get; internal set; }

        /// <summary>
        /// Metadata for the wrapping provider that can be used to unwrap the wrapped data encryption key.
        /// </summary>
        [JsonProperty(PropertyName = "keyWrapMetadata", NullValueHandling = NullValueHandling.Ignore)]
        public EncryptionKeyWrapMetadata EncryptionKeyWrapMetadata { get; internal set; }

        /// <summary>
        /// Gets the creation time of the resource from the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = "createTime", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? CreatedTime { get; internal set; }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        [JsonProperty(PropertyName = "_etag", NullValueHandling = NullValueHandling.Ignore)]
        public string ETag { get; internal set; }

        /// <summary>
        /// Gets the last modified time stamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = "_ts", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastModified { get; internal set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = "_self", NullValueHandling = NullValueHandling.Ignore)]
        public virtual string SelfLink { get; internal set; }

        /// <summary>
        /// Gets the Resource Id associated with the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Resource Id associated with the resource.
        /// </value>
        /// <remarks>
        /// A Resource Id is the unique, immutable, identifier assigned to each Azure Cosmos DB 
        /// resource whether that is a database, a collection or a document.
        /// These resource ids are used when building up SelfLinks, a static addressable Uri for each resource within a database account.
        /// </remarks>
        [JsonProperty(PropertyName = "_rid", NullValueHandling = NullValueHandling.Ignore)]
        internal string ResourceId { get; set; }

        /// <summary>
        /// Compares this instance of data encryption key properties to another object.
        /// </summary>
        /// <param name="obj">Object to compare with.</param>
        /// <returns>True if the other object is an instance of <see cref="DataEncryptionKeyProperties"/> and the properties match, else false.</returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as DataEncryptionKeyProperties);
        }

        /// <summary>
        /// Compares this instance of data encryption key properties to another.
        /// </summary>
        /// <param name="other">Instance to compare with.</param>
        /// <returns>True if properties match, else false.</returns>
        public bool Equals(DataEncryptionKeyProperties other)
        {
            return other != null &&
                   this.Id == other.Id &&
                   this.EncryptionAlgorithm == other.EncryptionAlgorithm &&
                   DataEncryptionKeyProperties.Equals(this.WrappedDataEncryptionKey, other.WrappedDataEncryptionKey) &&
                   EqualityComparer<EncryptionKeyWrapMetadata>.Default.Equals(this.EncryptionKeyWrapMetadata, other.EncryptionKeyWrapMetadata) &&
                   this.CreatedTime == other.CreatedTime &&
                   this.ETag == other.ETag &&
                   this.LastModified == other.LastModified &&
                   this.SelfLink == other.SelfLink &&
                   this.ResourceId == other.ResourceId;
        }

        /// <summary>
        /// Gets a hash code for the properties of this instance to optimize comparisons.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode()
        {
            int hashCode = -1673632966;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.EncryptionAlgorithm);
            hashCode = (hashCode * -1521134295) + EqualityComparer<EncryptionKeyWrapMetadata>.Default.GetHashCode(this.EncryptionKeyWrapMetadata);
            hashCode = (hashCode * -1521134295) + EqualityComparer<DateTime?>.Default.GetHashCode(this.CreatedTime);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.ETag);
            hashCode = (hashCode * -1521134295) + EqualityComparer<DateTime?>.Default.GetHashCode(this.LastModified);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.SelfLink);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.ResourceId);
            return hashCode;
        }

        private static bool Equals(byte[] x, byte[] y)
        {
            return (x == null && y == null)
                || (x != null && y != null && x.SequenceEqual(y));
        }
    }
}