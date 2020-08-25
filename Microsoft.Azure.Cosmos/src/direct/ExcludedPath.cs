//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.ObjectModel;

    /// <summary> 
    /// Specifies a path within a JSON document to be excluded while indexing data for the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class ExcludedPath : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExcludedPath"/> class for the Azure Cosmos DB service.
        /// </summary>
        public ExcludedPath()
        {
        }

        /// <summary>
        /// Gets or sets the path to be excluded from indexing in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be excluded from indexing.
        /// </value>
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

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<string>(Constants.Properties.Path);
        }

        /// <summary>
        /// Creates a copy of the excluded path in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>A clone of the excluded path.</returns>
        public object Clone()
        {
            ExcludedPath cloned = new ExcludedPath()
            {
                Path = this.Path
            };

            return cloned;
        }
    }
}
