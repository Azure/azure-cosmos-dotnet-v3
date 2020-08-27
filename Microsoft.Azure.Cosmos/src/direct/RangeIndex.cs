//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Globalization;

    /// <summary>
    /// Specifies an instance of the <see cref="RangeIndex"/> class in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Can be used to serve queries like: SELECT * FROM docs d WHERE d.prop > 5.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class RangeIndex : Index, ICloneable
    {
        internal RangeIndex()
            : base(IndexKind.Range)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeIndex"/> class with specified DataType for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to instantiate RangeIndex class passing in the DataType:
        /// <code language="c#">
        /// <![CDATA[
        /// RangeIndex rangeIndex = new RangeIndex(DataType.Number);
        /// ]]>
        /// </code>
        /// </example>
        public RangeIndex(DataType dataType)
            : this()
        {
            this.DataType = dataType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeIndex"/> class with specified DataType and precision for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <param name="precision">Specifies the precision to be used for the data type associated with this index.</param>
        /// <seealso cref="DataType"/>
        /// <example>
        /// Here is an example to instantiate RangeIndex class passing in the DataType and precision:
        /// <code language="c#">
        /// <![CDATA[
        /// RangeIndex rangeIndex = new RangeIndex(DataType.Number, -1);
        /// ]]>
        /// </code>
        /// </example>
        public RangeIndex(DataType dataType, short precision)
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
        /// <remarks>Refer to http://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/#ConfigPolicy for valid ranges of values.</remarks>
        [JsonProperty(PropertyName = Constants.Properties.DataType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DataType DataType
        {
            get
            {
                DataType result = Documents.DataType.Number;
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

        /// <summary>
        /// Gets or sets the precision for this particular index in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The precision for this particular index. Returns null, if not set.
        /// </value>
        /// <remarks>Refer to http://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/#ConfigPolicy for valid ranges of values.</remarks>
        [JsonProperty(PropertyName = Constants.Properties.Precision,
            NullValueHandling = NullValueHandling.Ignore)]
        public short? Precision
        {
            get
            {
                short? result = null;
                string strValue = base.GetValue<string>(Constants.Properties.Precision);
                if (!string.IsNullOrEmpty(strValue))
                {
                    result = Convert.ToInt16(strValue, CultureInfo.InvariantCulture);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.Precision, value);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            Helpers.ValidateEnumProperties<DataType>(this.DataType);
            short? precision = this.Precision;
        }

        /// <summary>
        /// Creates a copy of the range index for the Azure Cosmos DB service.
        /// </summary>
        /// <returns>A clone of the range index.</returns>
        public object Clone()
        {
            return new RangeIndex(this.DataType)
            {
                Precision = this.Precision
            };
        }
    }
}