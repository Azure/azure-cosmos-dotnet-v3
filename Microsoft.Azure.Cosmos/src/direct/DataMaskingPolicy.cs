//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the data masking policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class DataMaskingPolicy : JsonSerializable
    {
        private Collection<DataMaskingIncludedPath> includedPaths;
        private Boolean isPolicyEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataMaskingPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        public DataMaskingPolicy()
        {
        }

        /// <summary>
        /// Paths of the item that need masked along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths)]
        public Collection<DataMaskingIncludedPath> IncludedPaths
        {
            get
            {
                if (this.includedPaths == null)
                {
                    this.includedPaths = base.GetObjectCollection<DataMaskingIncludedPath>(Constants.Properties.IncludedPaths);
                    if (this.includedPaths == null)
                    {
                        this.includedPaths = new Collection<DataMaskingIncludedPath>();
                    }
                }

                return this.includedPaths;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(IncludedPaths)));
                }

                this.includedPaths = value;
                base.SetObjectCollection(Constants.Properties.IncludedPaths, this.includedPaths);
            }
        }

        /// <summary>
        /// Paths of the item that need masked along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.IsPolicyEnabled)]
        public Boolean IsPolicyEnabled
        {
            get
            {
                if (!this.isPolicyEnabled)
                {
                    this.isPolicyEnabled = base.GetValue<Boolean>(Constants.Properties.IsPolicyEnabled, true);
                }

                return this.isPolicyEnabled;
            }
            set
            {
                if (!value)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(IsPolicyEnabled)));
                }

                this.isPolicyEnabled = value;
                base.SetValue(Constants.Properties.IsPolicyEnabled, this.IsPolicyEnabled);
            }
        }

        internal override void OnSave()
        {
            if (this.includedPaths != null)
            {
                base.SetObjectCollection(Constants.Properties.IncludedPaths, this.includedPaths);
                base.SetValue(Constants.Properties.IsPolicyEnabled, this.isPolicyEnabled);
            }
        }
    }
}
