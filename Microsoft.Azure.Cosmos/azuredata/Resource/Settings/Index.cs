//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    /// <summary>
    /// Base class for IndexingPolicy Indexes in the Azure Cosmos DB service, you should use a concrete Index like HashIndex or RangeIndex.
    /// </summary> 
    internal abstract class Index
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Index"/> class for the Azure Cosmos DB service.
        /// </summary>
        protected Index(IndexKind kind)
        {
            this.Kind = kind;
        }

        /// <summary>
        /// Gets or sets the kind of indexing to be applied in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.IndexKind"/> enumeration.
        /// </value>
        public IndexKind Kind { get; set; }

        /// <summary>
        /// Returns an instance of the <see cref="RangeIndex"/> class with specified DataType for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <returns>An instance of <see cref="RangeIndex"/> type.</returns>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to create RangeIndex instance passing in the DataType:
        /// <code language="c#">
        /// <![CDATA[
        /// RangeIndex rangeIndex = Index.Range(DataType.Number);
        /// ]]>
        /// </code>
        /// </example>
        public static RangeIndex Range(DataType dataType)
        {
            return new RangeIndex(dataType);
        }

        /// <summary>
        /// Returns an instance of the <see cref="RangeIndex"/> class with specified DataType and precision for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <param name="precision">Specifies the precision to be used for the data type associated with this index.</param>
        /// <returns>An instance of <see cref="RangeIndex"/> type.</returns>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to create RangeIndex instance passing in the DataType and precision:
        /// <code language="c#">
        /// <![CDATA[
        /// RangeIndex rangeIndex = Index.Range(DataType.Number, -1);
        /// ]]>
        /// </code>
        /// </example>
        public static RangeIndex Range(DataType dataType, short precision)
        {
            return new RangeIndex(dataType, precision);
        }

        /// <summary>
        /// Returns an instance of the <see cref="HashIndex"/> class with specified DataType for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <returns>An instance of <see cref="HashIndex"/> type.</returns>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to create HashIndex instance passing in the DataType:
        /// <code language="c#">
        /// <![CDATA[
        /// HashIndex hashIndex = Index.Hash(DataType.String);
        /// ]]>
        /// </code>
        /// </example>
        public static HashIndex Hash(DataType dataType)
        {
            return new HashIndex(dataType);
        }

        /// <summary>
        /// Returns an instance of the <see cref="HashIndex"/> class with specified DataType and precision for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <param name="precision">Specifies the precision to be used for the data type associated with this index.</param>
        /// <returns>An instance of <see cref="HashIndex"/> type.</returns>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to create HashIndex instance passing in the DataType and precision:
        /// <code language="c#">
        /// <![CDATA[
        /// HashIndex hashIndex = Index.Hash(DataType.String, 3);
        /// ]]>
        /// </code>
        /// </example>
        public static HashIndex Hash(DataType dataType, short precision)
        {
            return new HashIndex(dataType, precision);
        }

        /// <summary>
        /// Returns an instance of the <see cref="SpatialIndex"/> class with specified DataType for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <returns>An instance of <see cref="SpatialIndex"/> type.</returns>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to create SpatialIndex instance passing in the DataType:
        /// <code language="c#">
        /// <![CDATA[
        /// SpatialIndex spatialIndex = Index.Spatial(DataType.Point);
        /// ]]>
        /// </code>
        /// </example>
        public static SpatialIndex Spatial(DataType dataType)
        {
            return new SpatialIndex(dataType);
        }
    }
}