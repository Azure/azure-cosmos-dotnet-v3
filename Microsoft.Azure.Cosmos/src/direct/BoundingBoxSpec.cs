//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;

    /// <summary>
    /// Represents bounding box for geometry spatial path in the Azure Cosmos DB service
    /// </summary>
    /// <example>
    /// <![CDATA[
    ///    {
    ///       "id": "DocumentCollection Id",
    ///       "indexingPolicy":{
    ///             "spatialIndexes":
    ///             [{  
    ///                 "path":"/'region'/?",
    ///                 "types":["Polygon"],
    ///                 "boundingBox": 
    ///                 {
    ///                    "xmin":0, 
    ///                    "ymin":0,
    ///                    "xmax":10, 
    ///                    "ymax":10
    ///                 }
    ///             }]
    ///            },
    ///       "geospatialConfig": 
    ///       {
    ///           "type": "Geometry"
    ///       }
    ///    }
    /// ]]>
    /// </example>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class BoundingBoxSpec : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Gets the x-coordinate of the lower-left corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Xmin)]
        public double Xmin
        {
            get
            {
                return base.GetValue<double>(Constants.Properties.Xmin);
            }
            set
            {
                base.SetValue(Constants.Properties.Xmin, value);
            }
        }

        /// <summary>
        /// Gets the y-coordinate of the lower-left corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Ymin)]
        public double Ymin
        {
            get
            {
                return base.GetValue<double>(Constants.Properties.Ymin);
            }
            set
            {
                base.SetValue(Constants.Properties.Ymin, value);
            }
        }

        /// <summary>
        /// Gets the x-coordinate of the upper-right corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Xmax)]
        public double Xmax
        {
            get
            {
                return base.GetValue<double>(Constants.Properties.Xmax);
            }
            set
            {
                base.SetValue(Constants.Properties.Xmax, value);
            }
        }

        /// <summary>
        /// Gets the y-coordinate of the upper-right corner of the bounding box.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Ymax)]
        public double Ymax
        {
            get
            {
                return base.GetValue<double>(Constants.Properties.Ymax);
            }
            set
            {
                base.SetValue(Constants.Properties.Ymax, value);
            }
        }

        public object Clone()
        {
            BoundingBoxSpec cloned = new BoundingBoxSpec();
            cloned.Xmin = this.Xmin;
            cloned.Ymin = this.Ymin;
            cloned.Xmax = this.Xmax;
            cloned.Ymax = this.Ymax;

            return cloned;
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<double>(Constants.Properties.Xmin);
            base.GetValue<double>(Constants.Properties.Ymin);
            base.GetValue<double>(Constants.Properties.Xmax);
            base.GetValue<double>(Constants.Properties.Ymax);
        }
    }
}