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
    /// Represents the FullText Policy on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class FullTextPolicy : JsonSerializable
    {
        private Collection<FullTextPath> fullTextPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTextPolicy"/> class.
        /// </summary>
        public FullTextPolicy()
        {
        }

        /// <summary>
        /// Gets or sets a string containing the default language.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DefaultLanguage, NullValueHandling = NullValueHandling.Ignore)]
        public string DefaultLanguage
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.DefaultLanguage);
            }
            set
            {
                base.SetValue(Constants.Properties.DefaultLanguage, value);
            }
        }

        /// <summary>
        /// Gets a collection of <see cref="FullTextPath"/> that contains the fullTextPaths of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.FullTextPaths, NullValueHandling = NullValueHandling.Ignore)]
        public Collection<FullTextPath> FullTextPaths
        {
            get
            {
                if (this.fullTextPaths == null)
                {
                    this.fullTextPaths = base.GetObjectCollection<FullTextPath>(Constants.Properties.FullTextPaths);
                    if (this.fullTextPaths == null)
                    {
                        this.fullTextPaths = new Collection<FullTextPath>();
                    }
                }

                return this.fullTextPaths;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(fullTextPaths)));
                }

                this.fullTextPaths = value;
                this.SetValue(Constants.Properties.FullTextPaths, this.fullTextPaths);
            }
        }

        internal override void OnSave()
        {
            this.SetValue(Constants.Properties.DefaultLanguage, this.DefaultLanguage);

            if (this.fullTextPaths != null)
            {
                base.SetObjectCollection(Constants.Properties.FullTextPaths, this.fullTextPaths);
            }
        }
    }
}
