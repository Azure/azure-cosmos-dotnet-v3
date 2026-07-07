//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the default specification for full-text paths in a <see cref="FullTextPolicy"/>.
    /// Fields set here are inherited by full-text paths that do not override them.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class FullTextDefaultSpec
    {
        /// <summary>
        /// Gets or sets the default language locale (e.g., "en-US").
        /// </summary>
        [JsonProperty(PropertyName = "language", NullValueHandling = NullValueHandling.Ignore)]
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer method. Valid values: "word".
        /// Only applicable with the "standard" package.
        /// </summary>
        [JsonProperty(PropertyName = "tokenizer", NullValueHandling = NullValueHandling.Ignore)]
        public string Tokenizer { get; set; }

        /// <summary>
        /// Gets or sets the filter pipeline. Valid values: "stop", "lowercase", "stem", "ascii".
        /// Only applicable with the "standard" package and tokenizer "word".
        /// </summary>
        [JsonProperty(PropertyName = "filters", NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> Filters { get; set; }

        /// <summary>
        /// Gets or sets the stop word list kind. Valid values: "none", "basic", "extended".
        /// </summary>
        [JsonProperty(PropertyName = "stopWordListKind", NullValueHandling = NullValueHandling.Ignore)]
        public string StopWordListKind { get; set; }

        /// <summary>
        /// Gets or sets the custom stop words to add to the stop word list.
        /// </summary>
        [JsonProperty(PropertyName = "addStopWords", NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> AddStopWords { get; set; }

        /// <summary>
        /// Gets or sets the stop words to remove from the built-in stop word list.
        /// </summary>
        [JsonProperty(PropertyName = "removeStopWords", NullValueHandling = NullValueHandling.Ignore)]
        public Collection<string> RemoveStopWords { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields.
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
