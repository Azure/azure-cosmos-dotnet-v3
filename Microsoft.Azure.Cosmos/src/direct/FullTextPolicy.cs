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
        private FullTextSpec defaultSpec;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTextPolicy"/> class.
        /// </summary>
        public FullTextPolicy()
        {
        }

        /// <summary>
        /// Gets or sets a string containing the full text package type (e.g. "legacy" or "standard").
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.FullTextPackage, NullValueHandling = NullValueHandling.Ignore)]
        public string Package
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.FullTextPackage);
            }
            set
            {
                base.SetValue(Constants.Properties.FullTextPackage, value);
            }
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
        /// Gets or sets the default full text analysis specification inherited by fullTextPaths that omit fields.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.FullTextDefaultSpec, NullValueHandling = NullValueHandling.Ignore)]
        public FullTextSpec DefaultSpec
        {
            get
            {
                if (this.defaultSpec == null)
                {
                    this.defaultSpec = base.GetObject<FullTextSpec>(Constants.Properties.FullTextDefaultSpec);
                }

                return this.defaultSpec;
            }
            set
            {
                this.defaultSpec = value;
                base.SetObject(Constants.Properties.FullTextDefaultSpec, value);
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
            this.SetValue(Constants.Properties.FullTextPackage, this.Package);

            if (this.defaultSpec != null)
            {
                this.defaultSpec.OnSave();
                base.SetObject(Constants.Properties.FullTextDefaultSpec, this.defaultSpec);
            }

            if (this.fullTextPaths != null)
            {
                base.SetObjectCollection(Constants.Properties.FullTextPaths, this.fullTextPaths);
            }
        }
    }
}
