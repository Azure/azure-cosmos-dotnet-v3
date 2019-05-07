//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a stored procedure in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// Azure Cosmos DB allows application logic written entirely in JavaScript to be executed directly inside the database engine under the database transaction.
    /// For additional details, refer to the server-side JavaScript API documentation.
    /// </remarks>
    public class CosmosStoredProcedureSettings
    {
        /// <summary>
        /// Initializes a new instance of the Stored Procedure class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosStoredProcedureSettings()
        {
        }

        /// <summary>
        /// Gets or sets the body of the Azure Cosmos DB stored procedure.
        /// </summary>
        /// <value>The body of the stored procedure.</value>
        /// <remarks>Must be a valid JavaScript function. For e.g. "function () { getContext().getResponse().setBody('Hello World!'); }"</remarks>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public virtual string Body { get; set; }

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
        public virtual string Id { get; set; }

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
        public virtual string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified timestamp associated with <see cref="CosmosStoredProcedureSettings" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified timestamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified)]
        public virtual DateTime? LastModified { get; private set; }

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
        internal virtual string ResourceId { get; private set; }
    }
}
