//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents details of the hash index setting in an Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Can be used to serve queries like: SELECT * FROM docs d WHERE d.prop = 5.
    /// </remarks>
    internal sealed class HashIndex : Index, ICloneable
    {
        internal HashIndex()
            : base(IndexKind.Hash)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HashIndex"/> class with specified DataType for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to instantiate HashIndex class passing in the DataType:
        /// <code language="c#">
        /// <![CDATA[
        /// HashIndex hashIndex = new HashIndex(DataType.String);
        /// ]]>
        /// </code>
        /// </example>
        public HashIndex(DataType dataType)
            : this()
        {
            this.DataType = dataType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HashIndex"/> class with specified DataType and precision for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <param name="precision">Specifies the precision to be used for the data type associated with this index.</param>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to instantiate HashIndex class passing in the DataType and precision:
        /// <code language="c#">
        /// <![CDATA[
        /// HashIndex hashIndex = new HashIndex(DataType.String, 3);
        /// ]]>
        /// </code>
        /// </example>
        public HashIndex(DataType dataType, short precision)
            : this(dataType)
        {
            this.Precision = precision;
        }

        /// <summary>
        /// Gets or sets the data type for which this index should be applied in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The data type for which this index should be applied.
        /// </value>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/index-policy"/>
        [JsonPropertyName(Constants.Properties.DataType)]
        [JsonConverter(typeof(JsonStringEnumConverter<DataType>))]
        public DataType DataType { get; set; }

        /// <summary>
        /// Gets or sets the precision for this particular index in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The precision for this particular index. Returns null, if not set.
        /// </value>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/index-policy"/>
        [JsonPropertyName(Constants.Properties.Precision)]
        public short? Precision { get; set; }

        /// <summary>
        /// Creates a copy of the hash index for the Azure Cosmos DB service.
        /// </summary>
        /// <returns>A clone of the hash index.</returns>
        public object Clone()
        {
            return new HashIndex(this.DataType)
            {
                Precision = this.Precision
            };
        }
    }
}