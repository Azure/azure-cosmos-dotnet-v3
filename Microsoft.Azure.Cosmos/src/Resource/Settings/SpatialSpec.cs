//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal sealed class SpatialSpec : JsonSerializable, ICloneable
    {
        private Collection<SpatialType> spatialTypes;

        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Path);
            }
            set
            {
                base.SetValue(Constants.Properties.Path, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Types, ItemConverterType = typeof(StringEnumConverter))]
        public Collection<SpatialType> SpatialTypes
        {
            get
            {
                if (this.spatialTypes == null)
                {
                    this.spatialTypes = base.GetValue<Collection<SpatialType>>(Constants.Properties.Types);

                    if (this.spatialTypes == null)
                    {
                        this.spatialTypes = new Collection<SpatialType>();
                    }
                }

                return this.spatialTypes;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, RMResources.PropertyCannotBeNull, "SpatialTypes"));
                }

                this.spatialTypes = value;
                base.SetValue(Constants.Properties.Types, value);
            }
        }

        public object Clone()
        {
            SpatialSpec cloned = new SpatialSpec()
            {
                Path = this.Path
            };

            foreach (SpatialType spatialType in this.SpatialTypes)
            {
                cloned.SpatialTypes.Add(spatialType);
            }

            return cloned;
        }

        internal override void OnSave()
        {
            if (this.spatialTypes != null)
            {
                base.SetValue(Constants.Properties.Types, this.spatialTypes);
            }
        }
    }
}
