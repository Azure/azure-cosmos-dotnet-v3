//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a stored procedure in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// Azure Cosmos DB allows application logic written entirely in JavaScript to be executed directly inside the database engine under the database transaction.
    /// For additional details, refer to the server-side JavaScript API documentation.
    /// </remarks>
    public class StoredProcedureProperties
    {
        private string id;
        private string body;

        /// <summary>
        /// Initializes a new instance of the Stored Procedure class for the Azure Cosmos DB service.
        /// </summary>
        public StoredProcedureProperties()
        {
        }

        /// <summary>
        /// Initializes a new instance of the Stored Procedure class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The Id of the resource in the Azure Cosmos service.</param>
        /// <param name="body">The body of the Azure Cosmos DB stored procedure.</param>
        public StoredProcedureProperties(
            string id,
            string body)
        {
            this.Id = id;
            this.Body = body;
        }

        /// <summary>
        /// Gets or sets the body of the Azure Cosmos DB stored procedure.
        /// </summary>
        /// <value>The body of the stored procedure.</value>
        /// <remarks>Must be a valid JavaScript function. For e.g. "function () { getContext().getResponse().setBody('Hello World!'); }"</remarks>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body
        {
            get => this.body;
            set => this.body = value ?? throw new ArgumentNullException(nameof(this.Body));
        }

        /// <summary>
        /// Gets or sets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
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
        public string Id
        {
            get => this.id;
            set => this.id = value ?? throw new ArgumentNullException(nameof(this.Id));
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
        [JsonProperty(PropertyName = Constants.Properties.ETag, NullValueHandling = NullValueHandling.Ignore)]
        public string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified timestamp associated with <see cref="StoredProcedureProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified timestamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastModified { get; private set; }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink, NullValueHandling = NullValueHandling.Ignore)]
        public string SelfLink { get; private set; }

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
        internal string ResourceId { get; private set; }

        /// <summary>
        /// It will contain Additional Properties which are not part of current contract
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
