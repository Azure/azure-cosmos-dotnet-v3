//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary> 
    /// Specifies a path within a JSON document to be included in the Azure Cosmos DB service.
    /// </summary>
    public sealed class IncludedPath
    {
        /// <summary>
        /// Gets or sets the path to be indexed in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be indexed.
        /// </value>
        /// <remarks>
        /// Some valid examples: /"prop"/?, /"prop"/**, /"prop"/"subprop"/?, /"prop"/[]/?
        /// </remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/index-policy"/>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the collection of <see cref="Index"/> objects to be applied for this included path in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection of the <see cref="Index"/> objects to be applied for this included path.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Indexes)]
        internal Collection<Cosmos.Index> Indexes { get; set; } = new Collection<Cosmos.Index>();

        /// <summary>
        /// Gets or sets whether this is a full index used for collection types.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.IsFullIndex, NullValueHandling = NullValueHandling.Ignore)]
        internal bool? IsFullIndex { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
