//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;

    /// <summary>
    /// Specifies an instance of the <see cref="SpatialIndex"/> class in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Can be used to serve spatial queries.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class SpatialIndex : Index, ICloneable
    {
        internal SpatialIndex()
            : base(IndexKind.Spatial)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpatialIndex"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification</param>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to instantiate SpatialIndex class passing in the DataType
        /// <code language="c#">
        /// <![CDATA[
        /// SpatialIndex spatialIndex = new SpatialIndex(DataType.Point);
        /// ]]>
        /// </code>
        /// </example>
        public SpatialIndex(DataType dataType)
            : this()
        {
            this.DataType = dataType;
        }

        /// <summary>
        /// Gets or sets the data type for which this index should be applied in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The data type for which this index should be applied.
        /// </value>
        /// <remarks>Refer to http://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/#ConfigPolicy for valid ranges of values.</remarks>
        [JsonProperty(PropertyName = Constants.Properties.DataType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DataType DataType
        {
            get
            {
                DataType result = default(DataType);
                string strValue = base.GetValue<string>(Constants.Properties.DataType);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = (DataType)Enum.Parse(typeof(DataType), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.DataType, value.ToString());
            }
        }

        internal override void Validate()
        {
            base.Validate();
            Helpers.ValidateEnumProperties<DataType>(this.DataType);
        }

        /// <summary>
        /// Creates a copy of the spatial index for the Azure Cosmos DB service.
        /// </summary>
        /// <returns>A clone of the spatial index.</returns>
        public object Clone()
        {
            return new SpatialIndex(this.DataType);
        }
    }
}