//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
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
        /// <seealso cref="CosmosDataType"/>
        /// <example>
        /// Here is an example to instantiate SpatialIndex class passing in the DataType
        /// <code language="c#">
        /// <![CDATA[
        /// SpatialIndex spatialIndex = new SpatialIndex(CosmosDataType.Point);
        /// ]]>
        /// </code>
        /// </example>
        public SpatialIndex(CosmosDataType dataType)
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
        public CosmosDataType DataType { get; set; }
    }
}
