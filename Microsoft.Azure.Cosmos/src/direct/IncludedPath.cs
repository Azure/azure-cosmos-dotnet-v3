//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary> 
    /// Specifies a path within a JSON document to be included in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class IncludedPath : JsonSerializable, ICloneable
    {
        private Collection<Index> indexes;

        /// <summary>
        /// Initializes a new instance of the <see cref="IncludedPath"/> class for the Azure Cosmos DB service.
        /// </summary>
        public IncludedPath()
        {
        }

        /// <summary>
        /// Gets or sets the path to be indexed in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be indexed.
        /// </value>
        /// <remarks>
        /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/#ConfigPolicy for how to specify paths.
        /// Some valid examples: /"prop"/?, /"prop"/**, /"prop"/"subprop"/?, /"prop"/[]/?
        /// </remarks>
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

        /// <summary>
        /// Gets or sets the collection of <see cref="Index"/> objects to be applied for this included path in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection of the <see cref="Index"/> objects to be applied for this included path.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Indexes)]
        public Collection<Index> Indexes
        {
            get
            {
                if (this.indexes == null)
                {
                    this.indexes = base.GetValue<Collection<Index>>(Constants.Properties.Indexes);

                    if (this.indexes == null)
                    {
                        this.indexes = new Collection<Index>();
                    }
                }

                return this.indexes;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, RMResources.PropertyCannotBeNull, "Indexes"));
                }

                this.indexes = value;
                base.SetValue(Constants.Properties.Indexes, value);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<string>(Constants.Properties.Path);
            foreach (Index index in this.Indexes)
            {
                index.Validate();
            }
        }

        internal override void OnSave()
        {
            if (this.indexes != null)
            {
                foreach (Index index in this.indexes)
                {
                    index.OnSave();
                }

                base.SetValue(Constants.Properties.Indexes, this.indexes);
            }
        }

        /// <summary>
        /// Creates a copy of the included path in the Azure Cosmos DB service. 
        /// </summary>
        /// <returns>A clone of the included path.</returns>
        public object Clone()
        {
            IncludedPath cloned = new IncludedPath()
            {
                Path = this.Path
            };

            foreach (Index item in this.Indexes)
            {
                cloned.Indexes.Add(item);
            }

            return cloned;
        }
    }
}
