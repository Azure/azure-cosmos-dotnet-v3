//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;

    /// <summary>
    /// Represents the schema discovery policy configuration for a collection.
    /// </summary> 
    /// <remarks>
    /// The schema discovery policy is used to control the schema builder through a collection configuration.
    /// </remarks>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class SchemaDiscoveryPolicy : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaDiscoveryPolicy"/> class.
        /// </summary>
        /// <remarks>
        /// Schema mode is set to none
        /// </remarks>
        public SchemaDiscoveryPolicy()
        {
            this.SchemaBuilderMode = SchemaBuilderMode.None;
        }

        /// <summary>
        /// Gets or sets the indexing mode (consistent or lazy).
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.SchemaBuilderMode"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.SchemaBuilderMode)]
        [JsonConverter(typeof(StringEnumConverter))]
        public SchemaBuilderMode SchemaBuilderMode
        {
            get
            {
                SchemaBuilderMode result = SchemaBuilderMode.Lazy;
                string strValue = base.GetValue<string>(Constants.Properties.SchemaBuilderMode);
                if(!string.IsNullOrEmpty(strValue))
                {
                    result = (SchemaBuilderMode)Enum.Parse(typeof(SchemaBuilderMode), strValue, true);
                }
                return result;
            }
            set
            {
                base.SetValue(Constants.Properties.SchemaBuilderMode, value.ToString());
            }
        }

        /// <summary>
        /// Performs a deep copy of the schema discovery policy.
        /// </summary>
        /// <returns>
        /// A clone of the schema discovery policy.
        /// </returns>
        public object Clone()
        {
            SchemaDiscoveryPolicy cloned = new SchemaDiscoveryPolicy()
            {
            };

            return cloned;
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<string>(Constants.Properties.SchemaBuilderMode);
        }
    }
}
