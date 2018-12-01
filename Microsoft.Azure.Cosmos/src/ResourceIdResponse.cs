//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class ResourceIdResponse : CosmosResource
    {
        public ResourceIdResponse()
        {
        }

        [JsonProperty(PropertyName = Constants.Properties.ResourceId)]
        public string NewResourceId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ResourceId);
            }
            internal set
            {
                base.SetValue(Constants.Properties.ResourceId, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.PartitionIndex)]
        public int PartitionIndex
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.PartitionIndex);
            }
            internal set
            {
                base.SetValue(Constants.Properties.PartitionIndex, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.ServiceIndex)]
        public int ServiceIndex
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.ServiceIndex);
            }
            internal set
            {
                base.SetValue(Constants.Properties.ServiceIndex, value);
            }
        }
    }
}
