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

        [JsonProperty(PropertyName = Constants.Properties.SourceCollectionId, NullValueHandling = NullValueHandling.Ignore)]
        public string SourceCollectionId
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.SourceCollectionId);
            }
            set
            {
                this.SetValue(Constants.Properties.SourceCollectionId, value);
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

        // This property is used for identifying the collection as cold storage tier collection
        [JsonProperty(PropertyName = Constants.Properties.MaterializedViewContainerType, NullValueHandling = NullValueHandling.Ignore)]
        public string ContainerType
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.MaterializedViewContainerType);
            }
            set
            {
                this.SetValue(Constants.Properties.MaterializedViewContainerType, value);
            }
        }

        public object Clone()
        {
            MaterializedViewDefinition cloned = new MaterializedViewDefinition()
            {
                SourceCollectionRid = this.SourceCollectionRid,
                Definition = this.Definition,
                ApiSpecificDefinition = this.ApiSpecificDefinition,
                ContainerType = this.ContainerType
            };
            return cloned;
        }
    }
}
