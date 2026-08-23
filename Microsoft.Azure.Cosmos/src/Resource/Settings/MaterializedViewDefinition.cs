//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class MaterializedViewDefinition
    {
        [JsonConstructor]
        internal MaterializedViewDefinition()
        {
        }

        [JsonProperty(PropertyName = "sourceCollectionRid", NullValueHandling = NullValueHandling.Ignore)]
        internal string SourceContainerResourceId { get; set; }

        [JsonProperty(PropertyName = "sourceCollectionId", NullValueHandling = NullValueHandling.Ignore)]
        internal string SourceContainerId { get; set; }

        [JsonProperty(PropertyName = "definition", NullValueHandling = NullValueHandling.Ignore)]
        internal string Definition { get; set; }

        [JsonProperty(PropertyName = "apiSpecificDefinition", NullValueHandling = NullValueHandling.Ignore)]
        internal string ApiSpecificDefinition { get; set; }

        [JsonProperty(PropertyName = "containerType", NullValueHandling = NullValueHandling.Ignore)]
        internal string ContainerType { get; set; }

        [JsonProperty(PropertyName = "status", NullValueHandling = NullValueHandling.Ignore)]
        internal string Status { get; set; }

        [JsonProperty(PropertyName = "throughputBucketForBuild", NullValueHandling = NullValueHandling.Ignore)]
        internal int? ThroughputBucketForBuild { get; set; }

        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
