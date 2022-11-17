//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Spatial index specification
    /// </summary>
    /// <example>
    /// <![CDATA[
    ///     "spatialIndexes":
    ///     [
    ///         {  
    ///             "path":"/'region'/?",
    ///             "types":["Polygon"],
    ///             "boundingBox": 
    ///                 {
    ///                    "xmin":0, 
    ///                    "ymin":0,
    ///                    "xmax":10, 
    ///                    "ymax":10
    ///                 }
    ///        }
    ///   ]
    /// ]]>
    /// </example>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class SpatialSpec : JsonSerializable
    {
        private Collection<SpatialType> spatialTypes;
        private BoundingBoxSpec boundingBoxSpec;

        /// <summary>
        /// Path in JSON document to index
        /// </summary>
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

        /// <summary>
        /// Path's spatial type
        /// </summary>
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

        /// <summary>
        /// Gets or sets the bounding box
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.BoundingBox,
            NullValueHandling = NullValueHandling.Ignore)]
        public BoundingBoxSpec BoundingBox
        {
            get
            {
                return base.GetValue<BoundingBoxSpec>(Constants.Properties.BoundingBox);
            }
            set
            {
                this.boundingBoxSpec = value;
                this.SetValue(Constants.Properties.BoundingBox, this.boundingBoxSpec);
            }
        }

        internal object Clone()
        {
            SpatialSpec cloned = new SpatialSpec()
            {
                Path = this.Path
            };

            foreach (SpatialType spatialType in this.SpatialTypes)
            {
                cloned.SpatialTypes.Add(spatialType);
            }

            if (this.boundingBoxSpec != null)
            {
                cloned.boundingBoxSpec = (BoundingBoxSpec)this.boundingBoxSpec.Clone();
            }                

            return cloned;
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<string>(Constants.Properties.Path);
            foreach (SpatialType spatialType in this.SpatialTypes)
            {
                Helpers.ValidateEnumProperties<SpatialType>(spatialType);
            }

            if (this.boundingBoxSpec != null)
            {
                boundingBoxSpec.Validate();
            }
        }

        internal override void OnSave()
        {
            if (this.spatialTypes != null)
            {
                base.SetValue(Constants.Properties.Types, this.spatialTypes);
            }

            if (this.boundingBoxSpec != null)
            {
                boundingBoxSpec.OnSave();
                base.SetValue(Constants.Properties.BoundingBox, this.boundingBoxSpec);
            }
            
        }
    }
}
