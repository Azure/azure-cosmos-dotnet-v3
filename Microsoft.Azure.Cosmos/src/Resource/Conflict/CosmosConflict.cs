//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a conflict in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// On rare occasions, during an async operation (insert, replace and delete), a version conflict may occur on a resource during failover or multi master scenarios.
    /// The conflicting resource is persisted as a Conflict resource.  
    /// Inspecting Conflict resources will allow you to determine which operations and resources resulted in conflicts.
    /// This is not related to operations returning a Conflict status code.
    /// </remarks>
    public class CosmosConflict
    {
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
        public virtual OperationKind OperationKind { get; set; }

        /// <summary>
        /// Gets or sets the type of the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(ConflictResourceTypeJsonConverter))]
        [JsonProperty(PropertyName = Documents.Constants.Properties.ResourceType)]
        public virtual Type ResourceType { get; set; }

        /// <summary>
        /// Gets the content of the Conflict resource in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type to use to deserialize the content.</typeparam>
        /// <param name="cosmosJsonSerializer">(Optional) <see cref="CosmosJsonSerializer"/> to use while parsing the content.</param>
        /// <returns></returns>
        public virtual T GetResource<T>(CosmosJsonSerializer cosmosJsonSerializer = null)
        {
            if (!string.IsNullOrEmpty(this.Content))
            {
                if (cosmosJsonSerializer == null)
                {
                    cosmosJsonSerializer = new CosmosDefaultJsonSerializer();
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(this.Content);
                        writer.Flush();
                        stream.Position = 0;
                        return cosmosJsonSerializer.FromStream<T>(stream);
                    }
                }
            }

            return default(T);
        }

        [JsonProperty(PropertyName = Documents.Constants.Properties.SourceResourceId)]
        internal string SourceResourceId { get; set; }

        [JsonProperty(PropertyName = Documents.Constants.Properties.Content)]
        internal string Content { get; set; }

        [JsonProperty(PropertyName = Documents.Constants.Properties.ConflictLSN)]
        internal long ConflictLSN { get; set; }
    }
}
