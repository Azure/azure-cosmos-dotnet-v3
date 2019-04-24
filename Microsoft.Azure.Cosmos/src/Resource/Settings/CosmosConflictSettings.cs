//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a conflict in the Azure Cosmos DB service.
    /// </summary>
    public class CosmosConflictSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:CosmosConflictSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosConflictSettings()
        {
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
        [JsonProperty(PropertyName = Documents.Constants.Properties.Id)]
        public virtual string Id { get; set; }

        /// <summary>
        /// Gets or sets the operation that resulted in the conflict in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Documents.Constants.Properties.OperationType)]
        public OperationKind OperationKind { get; set; }

        /// <summary>
        /// Gets or sets the type of the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Documents.Constants.Properties.ResourceType)]
        public Type ResourceType { get; set; }
    }
}
