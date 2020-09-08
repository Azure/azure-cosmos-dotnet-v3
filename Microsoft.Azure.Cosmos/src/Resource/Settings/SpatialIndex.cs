//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Specifies an instance of the <see cref="SpatialIndex"/> class in the Azure Cosmos DB service. 
    /// </summary>
    internal sealed class SpatialIndex : Index
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/index-policy"/>
        [JsonProperty(PropertyName = Constants.Properties.DataType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DataType DataType { get; set; }
    }
}
