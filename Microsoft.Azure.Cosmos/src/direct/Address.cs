//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    internal sealed class Address : Resource
    {
        [JsonProperty(PropertyName = Constants.Properties.IsPrimary)]
        public bool IsPrimary
        {
            get
            {
                return base.GetValue<bool>(Constants.Properties.IsPrimary);
            }
            internal set
            {
                base.SetValue(Constants.Properties.IsPrimary, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Protocol)]
        public string Protocol
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Protocol);
            }
            internal set
            {
                base.SetValue(Constants.Properties.Protocol, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.LogicalUri)]
        public string LogicalUri
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.LogicalUri);
            }
            internal set
            {
                base.SetValue(Constants.Properties.LogicalUri, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.PhysicalUri)]
        public string PhysicalUri
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.PhysicalUri);
            }
            internal set
            {
                base.SetValue(Constants.Properties.PhysicalUri, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.PartitionIndex)]
        public string PartitionIndex
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.PartitionIndex);
            }
            internal set
            {
                base.SetValue(Constants.Properties.PartitionIndex, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.PartitionKeyRangeId)]
        public string PartitionKeyRangeId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.PartitionKeyRangeId);
            }
            internal set
            {
                base.SetValue(Constants.Properties.PartitionKeyRangeId, value);
            }
        }
    }
}
