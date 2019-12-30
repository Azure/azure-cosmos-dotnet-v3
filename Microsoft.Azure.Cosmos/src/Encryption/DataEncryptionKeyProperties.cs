//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary> 
    /// Details of an encryption key for use with the Azure Cosmos DB service.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class DataEncryptionKeyProperties : IEquatable<DataEncryptionKeyProperties>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DataEncryptionKeyProperties"/>.
        /// </summary>
        /// <param name="id">Unique identifier for the data encryption key.</param>
        /// <param name="wrappedDataEncryptionKey">Wrapped (encrypted) form of the data encryption key.</param>
        /// <param name="keyWrapMetadata">Metadata used by the configured key wrapping provider in order to unwrap the key.</param>
        public DataEncryptionKeyProperties(
            string id,
            byte[] wrappedDataEncryptionKey,
            KeyWrapMetadata keyWrapMetadata)
        {
            this.Id = id;
            this.WrappedDataEncryptionKey = wrappedDataEncryptionKey;
            this.KeyWrapMetadata = keyWrapMetadata;
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
            this.KeyWrapMetadata = new KeyWrapMetadata(source.KeyWrapMetadata);
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
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; internal set; }

        /// <summary>
        /// Wrapped form of the data encryption key.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.WrappedDataEncryptionKey, NullValueHandling = NullValueHandling.Ignore)]
        public byte[] WrappedDataEncryptionKey { get; internal set; }

        /// <summary>
        /// Metadata for the wrapping provider than can be used to unwrap the wrapped data encryption key.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.KeyWrapMetadata, NullValueHandling = NullValueHandling.Ignore)]
        public KeyWrapMetadata KeyWrapMetadata { get; internal set; }

        /// <summary>
        /// Gets the creation time of the resource from the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.CreatedTime, NullValueHandling = NullValueHandling.Ignore)]
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
        [JsonProperty(PropertyName = Constants.Properties.ETag, NullValueHandling = NullValueHandling.Ignore)]
        public string ETag { get; internal set; }

        /// <summary>
        /// Gets the last modified time stamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastModified { get; internal set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink, NullValueHandling = NullValueHandling.Ignore)]
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
        [JsonProperty(PropertyName = Constants.Properties.RId, NullValueHandling = NullValueHandling.Ignore)]
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
                   this.Equals(this.WrappedDataEncryptionKey, other.WrappedDataEncryptionKey) &&
                   EqualityComparer<KeyWrapMetadata>.Default.Equals(this.KeyWrapMetadata, other.KeyWrapMetadata) &&
                   this.Equals(this.CreatedTime, other.CreatedTime) &&
                   this.ETag == other.ETag &&
                   this.Equals(this.LastModified, other.LastModified) &&
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
            hashCode = (hashCode * -1521134295) + EqualityComparer<KeyWrapMetadata>.Default.GetHashCode(this.KeyWrapMetadata);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.ETag);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.SelfLink);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.ResourceId);
            return hashCode;
        }

        private bool Equals(byte[] x, byte[] y)
        {
            return (x == null && y == null)
                || (x != null && y != null && x.SequenceEqual(y));
        }

        private bool Equals(DateTime? x, DateTime? y)
        {
            return (!x.HasValue && !y.HasValue)
                || (x.HasValue && y.HasValue
                    && (x.Value - y.Value).Duration() < TimeSpan.FromSeconds(1));
        }
    }
}