﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a trigger in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// Azure Cosmos DB supports pre and post triggers written in JavaScript to be executed on creates, updates and deletes. 
    /// For additional details, refer to the server-side JavaScript API documentation.
    /// </remarks>
    public class TriggerProperties
    {
        /// <summary>
        /// Gets or sets the body of the trigger for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the trigger.</value>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets the type of the trigger for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The body of the trigger.</value>
        /// <seealso cref="TriggerType"/>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.TriggerType)]
        public TriggerType TriggerType { get; set; }

        /// <summary>
        /// Gets or sets the operation the trigger is associated with for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The operation the trigger is associated with.</value>
        /// <seealso cref="TriggerOperation"/>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.TriggerOperation)]
        public TriggerOperation TriggerOperation { get; set; }

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
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; set; }

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
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
