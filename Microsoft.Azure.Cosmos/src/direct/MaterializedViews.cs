//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    internal sealed class MaterializedViews : JsonSerializable, ICloneable
    {
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Id);
            }
            set
            {
                this.SetValue(Constants.Properties.Id, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.RId)]
        public string Rid
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.RId);
            }
            set
            {
                this.SetValue(Constants.Properties.RId, value);
            }
        }

        public object Clone()
        {
            MaterializedViews cloned = new MaterializedViews()
            {
                Id = this.Id,
                Rid = this.Rid,
            };
            return cloned;
        }
    }
}
