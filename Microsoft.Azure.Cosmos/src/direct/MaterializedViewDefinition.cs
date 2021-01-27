//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
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

        // This property is used and hence needs to be set only for non-SQL APIs
        [JsonProperty(PropertyName = Constants.Properties.ApiSpecificDefinition, NullValueHandling = NullValueHandling.Ignore)]
        public string ApiSpecificDefinition
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ApiSpecificDefinition);
            }
            set
            {
                this.SetValue(Constants.Properties.ApiSpecificDefinition, value);
            }
        }

        public object Clone()
        {
            MaterializedViewDefinition cloned = new MaterializedViewDefinition()
            {
                SourceCollectionRid = this.SourceCollectionRid,
                Definition = this.Definition,
                ApiSpecificDefinition = this.ApiSpecificDefinition
            };
            return cloned;
        }
    }
}
