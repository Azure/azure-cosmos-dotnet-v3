//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents computed property that belongs to a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class ComputedProperty : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Gets or sets the name of the computed property.
        /// </summary>
        /// <value>
        /// The name of the computed property.
        /// </value>
        /// <remarks>
        /// Name of the computed property should be chosen such that it does not collide with any existing or future document properties.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Name)]
        public string Name
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Name);
            }
            set
            {
                base.SetValue(Constants.Properties.Name, value);
            }
        }

        /// <summary>
        /// Gets or sets the query for the computed property.
        /// </summary>
        /// <value>
        /// The query used to evaluate the value for the computed property.
        /// </value>
        /// <remarks>
        /// For example:
        /// SELECT VALUE LOWER(c.firstName) FROM c
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.Query)]
        public string Query
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Query);
            }
            set
            {
                base.SetValue(Constants.Properties.Query, value);
            }
        }

        /// <summary/>
        internal override void Validate()
        {
            // See IncludedPath.cs implementation
            base.Validate();
            base.GetValue<string>(Constants.Properties.Name);
            base.GetValue<string>(Constants.Properties.Query);
        }

        /// <summary>
        /// Clones a ComputedProperty object
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            return new ComputedProperty
            {
                Name = this.Name,
                Query = this.Query
            };
        }
    }
}
