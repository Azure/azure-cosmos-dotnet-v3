//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// DOM for a full text index path. A full text index path is used in a full text index.
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
    ///             "path": "/embeddings/vector/*"
    ///         }
    ///     ],
    ///     "fullTextIndexes": [
    ///         {
    ///             "path": "/v1/path1",
    ///         },
    ///         {
    ///             "path": "/v2/path2/text2",
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
        [JsonPropertyName(Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields.
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}