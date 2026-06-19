//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// DOM for a full text path. A full text path is defined at the collection level.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// "fullTextPolicy":
    /// {
    ///     "defaultLanguage": "en-US"
    ///     "fullTextPaths": [
    ///         {
    ///             "path": "/text1"
    ///             "language": "1033"
    ///         },
    ///         {
    ///             "path": "/text2"
    ///             "language": "en-US",
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
    public class FullTextPath : IEquatable<FullTextPath>
    {
        /// <summary>
        /// Gets or sets a string containing the path of the full text index.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets a string containing the language of the full text path.
        /// </summary>
        [JsonProperty(PropertyName = "language", NullValueHandling = NullValueHandling.Ignore)]
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer method. Valid values: "word".
        /// Only applicable with the "standard" package.
        /// </summary>
        [JsonProperty(PropertyName = "tokenizer", NullValueHandling = NullValueHandling.Ignore)]
#if PREVIEW
        public
#else
        internal
#endif
        string Tokenizer { get; set; }

        /// <summary>
        /// Gets or sets the filter pipeline. Valid values: "stop", "lowercase", "stem", "ascii".
        /// Only applicable with the "standard" package and tokenizer "word".
        /// </summary>
        [JsonProperty(PropertyName = "filters", NullValueHandling = NullValueHandling.Ignore)]
#if PREVIEW
        public
#else
        internal
#endif
        Collection<string> Filters { get; set; }

        /// <summary>
        /// Gets or sets the stop word list kind. Valid values: "none", "basic", "extended".
        /// </summary>
        [JsonProperty(PropertyName = "stopWordListKind", NullValueHandling = NullValueHandling.Ignore)]
#if PREVIEW
        public
#else
        internal
#endif
        string StopWordListKind { get; set; }

        /// <summary>
        /// Gets or sets the custom stop words to add to the stop word list.
        /// </summary>
        [JsonProperty(PropertyName = "addStopWords", NullValueHandling = NullValueHandling.Ignore)]
#if PREVIEW
        public
#else
        internal
#endif
        Collection<string> AddStopWords { get; set; }

        /// <summary>
        /// Gets or sets the stop words to remove from the built-in stop word list.
        /// </summary>
        [JsonProperty(PropertyName = "removeStopWords", NullValueHandling = NullValueHandling.Ignore)]
#if PREVIEW
        public
#else
        internal
#endif
        Collection<string> RemoveStopWords { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields.
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        /// <summary>
        /// Ensures that the paths specified in the full text policy are valid.
        /// </summary>
        public void ValidateFullTextPath()
        {
            if (string.IsNullOrEmpty(this.Path))
            {
                throw new ArgumentException("Argument {0} can't be null or empty.", nameof(this.Path));
            }

            if (this.Path[0] != '/')
            {
                throw new ArgumentException("The argument {0} is not a valid path.", this.Path);
            }
        }

        /// <inheritdoc/>
        public bool Equals(FullTextPath that)
        {
            return this.Path.Equals(that.Path) && this.Language.Equals(that.Language);
        }
    }
}