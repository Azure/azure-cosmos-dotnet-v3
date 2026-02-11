//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
#if COSMOSCLIENT
    internal
#else
    internal
#endif

    sealed class MaterializedViewsProperties : JsonSerializable, ICloneable
    {
        [JsonProperty(PropertyName = Constants.Properties.ThroughputBucketForBuild, NullValueHandling = NullValueHandling.Ignore)]
        public int ThroughputBucketForBuild
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.ThroughputBucketForBuild);
            }
            set
            {
                this.SetValue(Constants.Properties.ThroughputBucketForBuild, value);
            }
        }

        public object Clone()
        {
            MaterializedViewsProperties cloned = new MaterializedViewsProperties()
            {
                ThroughputBucketForBuild = this.ThroughputBucketForBuild
            };
            return cloned;
        }
    }
}
