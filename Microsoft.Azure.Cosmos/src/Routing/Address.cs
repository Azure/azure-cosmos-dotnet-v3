//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class Address : CosmosResource
    {
        [JsonProperty(PropertyName = Constants.Properties.IsPrimary)]
        public bool IsPrimary { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.Protocol)]
        public string Protocol { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.LogicalUri)]
        public string LogicalUri { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.PhysicalUri)]
        public string PhysicalUri { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.PartitionIndex)]
        public string PartitionIndex { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.PartitionKeyRangeId)]
        public string PartitionKeyRangeId { get; set; }
    }
}
