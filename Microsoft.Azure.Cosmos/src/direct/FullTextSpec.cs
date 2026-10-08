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
    /// Represents the default full text analysis specification for a full text policy in the Azure Cosmos DB service.
    /// Fields set here are inherited by <see cref="FullTextPath"/> entries that omit them, per the full text policy inheritance rules.
    /// </summary>
    internal sealed class FullTextSpec : JsonSerializable
    {
        private Collection<string> filters;
        private Collection<string> addStopWords;
        private Collection<string> removeStopWords;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTextSpec"/> class.
        /// </summary>
        public FullTextSpec()
        {
        }

        /// <summary>
        /// Gets or sets a string containing the language of the full text analysis.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Language, NullValueHandling = NullValueHandling.Ignore)]
        public string Language
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Language);
            }
            set
            {
                base.SetValue(Constants.Properties.Language, value);
            }
        }

        /// <summary>
        /// Gets or sets a string containing the tokenizer method of the full text analysis.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.FullTextTokenizer, NullValueHandling = NullValueHandling.Ignore)]
        public string Tokenizer
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.FullTextTokenizer);
            }
            set
            {
                base.SetValue(Constants.Properties.FullTextTokenizer, value);
            }
        }

        /// <summary>
        /// Gets or sets a collection of strings containing the filter pipeline of the full text analysis.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.FullTextFilters, NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> Filters
        {
            get
            {
                if (this.filters == null)
                {
                    this.filters = base.GetValue<Collection<string>>(Constants.Properties.FullTextFilters);
                    if (this.filters == null)
                    {
                        this.filters = new Collection<string>();
                    }
                }

                return this.filters;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(this.Filters)));
                }

                this.filters = value;
                base.SetValue(Constants.Properties.FullTextFilters, this.filters);
            }
        }

        /// <summary>
        /// Gets or sets a string containing the stop word list kind of the full text analysis.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.StopWordListKind, NullValueHandling = NullValueHandling.Ignore)]
        public string StopWordListKind
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.StopWordListKind);
            }
            set
            {
                base.SetValue(Constants.Properties.StopWordListKind, value);
            }
        }

        /// <summary>
        /// Gets or sets a collection of strings containing custom words to add to the stop word list.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.AddStopWords, NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> AddStopWords
        {
            get
            {
                if (this.addStopWords == null)
                {
                    this.addStopWords = base.GetValue<Collection<string>>(Constants.Properties.AddStopWords);
                    if (this.addStopWords == null)
                    {
                        this.addStopWords = new Collection<string>();
                    }
                }

                return this.addStopWords;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(this.AddStopWords)));
                }

                this.addStopWords = value;
                base.SetValue(Constants.Properties.AddStopWords, this.addStopWords);
            }
        }

        /// <summary>
        /// Gets or sets a collection of strings containing words to remove from the built-in stop word list.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.RemoveStopWords, NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> RemoveStopWords
        {
            get
            {
                if (this.removeStopWords == null)
                {
                    this.removeStopWords = base.GetValue<Collection<string>>(Constants.Properties.RemoveStopWords);
                    if (this.removeStopWords == null)
                    {
                        this.removeStopWords = new Collection<string>();
                    }
                }

                return this.removeStopWords;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(this.RemoveStopWords)));
                }

                this.removeStopWords = value;
                base.SetValue(Constants.Properties.RemoveStopWords, this.removeStopWords);
            }
        }

        // The Filters/AddStopWords/RemoveStopWords getters lazily materialize an empty collection,
        // and JsonProperty.NullValueHandling.Ignore only suppresses nulls (not empty arrays). These
        // ShouldSerialize hooks keep the JsonConvert reflection path aligned with the wire path
        // (OnSave below) so an empty (or never-set) collection is omitted rather than emitted as [].
        public bool ShouldSerializeFilters()
        {
            return this.filters != null && this.filters.Count > 0;
        }

        public bool ShouldSerializeAddStopWords()
        {
            return this.addStopWords != null && this.addStopWords.Count > 0;
        }

        public bool ShouldSerializeRemoveStopWords()
        {
            return this.removeStopWords != null && this.removeStopWords.Count > 0;
        }

        internal override void OnSave()
        {
            if (this.filters != null && this.filters.Count > 0)
            {
                base.SetValue(Constants.Properties.FullTextFilters, this.filters);
            }

            if (this.addStopWords != null && this.addStopWords.Count > 0)
            {
                base.SetValue(Constants.Properties.AddStopWords, this.addStopWords);
            }

            if (this.removeStopWords != null && this.removeStopWords.Count > 0)
            {
                base.SetValue(Constants.Properties.RemoveStopWords, this.removeStopWords);
            }
        }
    }
}
