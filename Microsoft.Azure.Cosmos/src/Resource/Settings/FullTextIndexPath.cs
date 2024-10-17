//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// DOM for a vector index path. A vector index path is used in a vector index.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// "indexingPolicy": {
    ///     "includedPaths": [
    ///         {
    ///             "path": "/*"
    ///         }
    ///     ],
    ///     "excludedPaths": [
    ///         {
    ///         }
    ///     ],
    ///     "fullTextIndexes": [
    ///         {
    ///             "path": "/v1",
    ///         },
    ///         {
    ///             "path": "/v2",
    ///         },
    ///         {
    ///             "path": "/v3",
    ///         }
    ///     ]
    /// }
    /// ]]>
    /// </example>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class FullTextIndexPath
    {
        /// <summary>
        /// Gets or sets the full path in a document used for full text indexing.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields.
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}