//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary> 
    ///  Represents an abstract resource type in the Azure Cosmos DB service.
    ///  All Azure Cosmos DB resources, such as <see cref="Database"/>, <see cref="DocumentCollection"/>, and <see cref="Document"/> extend this abstract type.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    abstract class Resource : JsonSerializable
    {
        internal static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="Resource"/> class for the Azure Cosmos DB service.
        /// </summary>
        protected Resource()
        {

        }

        /// <summary>
        /// Copy constructor for a <see cref="Resource"/> used in the Azure Cosmos DB service.
        /// </summary>
        protected Resource(Resource resource)
        {
            this.Id = resource.Id;
            this.ResourceId = resource.ResourceId;
            this.SelfLink = resource.SelfLink;
            this.AltLink = resource.AltLink;
            this.Timestamp = resource.Timestamp;
            this.ETag = resource.ETag;
        }


        /// <summary>
        /// Gets or sets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// Unlike <see cref="Resource.ResourceId"/>, which is set internally, this Id is settable by the user and is not immutable.
        /// </para>
        /// <para>
        /// When working with document resources, they too have this settable Id property. 
        /// If an Id is not supplied by the user the SDK will automatically generate a new GUID and assign its value to this property before
        /// persisting the document in the database. 
        /// You can override this auto Id generation by setting the disableAutomaticIdGeneration parameter on the <see cref="Microsoft.Azure.Documents.Client.DocumentClient"/> instance to true.
        /// This will prevent the SDK from generating new Ids. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public virtual string Id
        {
            get
            {
                return this.GetValue<string>(Constants.Properties.Id);
            }
            set
            {
                this.SetValue(Constants.Properties.Id, value);
            }
        }

        /// <summary>
        /// Gets or sets the Resource Id associated with the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Resource Id associated with the resource.
        /// </value>
        /// <remarks>
        /// A Resource Id is the unique, immutable, identifier assigned to each Azure Cosmos DB 
        /// resource whether that is a database, a collection or a document.
        /// These resource ids are used when building up SelfLinks, a static addressable Uri for each resource within a database account.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.RId)]
        public virtual string ResourceId
        {
            get
            {
                return this.GetValue<string>(Constants.Properties.RId);
            }
            set
            {
                this.SetValue(Constants.Properties.RId, value);
            }
        }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink)]
        public string SelfLink
        {
            get
            {
                return this.GetValue<string>(Constants.Properties.SelfLink);
            }
            internal set
            {
                this.SetValue(Constants.Properties.SelfLink, value);
            }
        }

        /// <summary>
        /// Gets the alt-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The alt-link associated with the resource.</value>
        [JsonIgnore]
        public string AltLink
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the last modified timestamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified timestamp associated with the resource.</value>
        [JsonProperty(PropertyName = Constants.Properties.LastModified)]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public virtual DateTime Timestamp
        {
            get
            {
                // Add seconds to the unix start time
                return UnixStartTime.AddSeconds(this.GetValue<double>(Constants.Properties.LastModified));
            }
            internal set
            {
                this.SetValue(Constants.Properties.LastModified, (ulong)(value - UnixStartTime).TotalSeconds);
            }
        }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.ETag)]
        public string ETag
        {
            get
            {
                return this.GetValue<string>(Constants.Properties.ETag);
            }
            internal set
            {
                this.SetValue(Constants.Properties.ETag, value);
            }
        }

        /// <summary>
        /// Sets property value associated with the specified property name in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="propertyValue">The property value.</param>
        public void SetPropertyValue(string propertyName, object propertyValue)
        {
            base.SetValue(propertyName, propertyValue);
        }

        /// <summary>
        /// Gets property value associated with the specified property name from the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The property value.</returns>
        public T GetPropertyValue<T>(string propertyName)
        {
            return base.GetValue<T>(propertyName);
        }

        /// <summary>
        /// Validates the property, by calling it, in case of any errors exception is thrown
        /// </summary>
        internal override void Validate()
        {
            base.Validate();
            this.GetValue<string>(Constants.Properties.Id);
            this.GetValue<string>(Constants.Properties.RId);
            this.GetValue<string>(Constants.Properties.SelfLink);
            this.GetValue<double>(Constants.Properties.LastModified);
            this.GetValue<string>(Constants.Properties.ETag);
        }

        /// <summary>
        /// Serialize to a byte array via SaveTo for the Azure Cosmos DB service.
        /// </summary>
        public byte[] ToByteArray()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                this.SaveTo(ms);
                return ms.ToArray();
            }
        }
    }
}
