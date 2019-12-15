//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary> 
    /// Details of an encryption key for use with the Azure Cosmos DB service.
    /// </summary>
    public class DataEncryptionKeyProperties
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
        public string Id { get; }

        /// <summary>
        /// Wrapped form of the data encryption key.
        /// </summary>
        [JsonProperty(PropertyName = "wrappedDek")]
        public byte[] WrappedDataEncryptionKey { get; }

        /// <summary>
        /// Metadata for the wrapping provider than can be used to unwrap the wrapped data encryption key.
        /// </summary>
        [JsonProperty(PropertyName = "keyWrapMetadata")]
        public KeyWrapMetadata KeyWrapMetadata { get; }

        /// <summary>
        /// Gets the creation time of the resource from the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = "_cts", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? CreatedTime { get; }

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
        public string ETag { get; }

        /// <summary>
        /// Gets the last modified time stamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastModified { get; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink, NullValueHandling = NullValueHandling.Ignore)]
        public string SelfLink { get; }

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
        internal string ResourceId { get; }
    }
}