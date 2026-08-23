//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class MaterializedViewProperties
    {
        [JsonConstructor]
        internal MaterializedViewProperties()
        {
        }

        [JsonProperty(PropertyName = "id", NullValueHandling = NullValueHandling.Ignore)]
        internal string Id { get; set; }

        [JsonProperty(PropertyName = "_rid", NullValueHandling = NullValueHandling.Ignore)]
        internal string ResourceId { get; set; }

        [JsonProperty(PropertyName = "containerType", NullValueHandling = NullValueHandling.Ignore)]
        internal string ContainerType { get; set; }

        [JsonProperty(PropertyName = "requiredPathsInPreviousImage", NullValueHandling = NullValueHandling.Ignore)]
        internal IReadOnlyList<string> RequiredPathsInPreviousImage { get; set; }

        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
