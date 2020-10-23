namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
#if COSMOSCLIENT
    internal
#else
    internal
#endif

    sealed class MaterializedViewDefinition : JsonSerializable, ICloneable
    {
        [JsonProperty(PropertyName = Constants.Properties.SourceCollectionRid)]
        public string SourceCollectionRid
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.SourceCollectionRid);
            }
            set
            {
                this.SetValue(Constants.Properties.SourceCollectionRid, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Definition)]
        public string Definition
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Definition);
            }
            set
            {
                this.SetValue(Constants.Properties.Definition, value);
            }
        }

        public object Clone()
        {
            MaterializedViewDefinition cloned = new MaterializedViewDefinition()
            {
                SourceCollectionRid = this.SourceCollectionRid,
                Definition = this.Definition
            };
            return cloned;
        }
    }
}
