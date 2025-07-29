//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the full text policy configuration for specifying the full text paths on items in the container in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="ContainerProperties"/>
#if PREVIEW
    public
#else
    public
#endif
    sealed class FullTextPolicy
    {
        [System.Text.Json.Serialization.JsonPropertyName("fullTextPaths")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        private Collection<FullTextPath> fullTextPathsInternal;

        /// <summary>
        /// Gets or sets a string containing the default language of the container.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("defaultLanguage")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DefaultLanguage { get; set; }

        /// <summary>
        /// Gets or sets a collection of <see cref="FullTextPath"/> that contains the full text paths of documents in
        /// a collection in the Azure Cosmos DB service.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Collection<FullTextPath> FullTextPaths
        {
            get => this.fullTextPathsInternal;
            set
            {
                FullTextPolicy.ValidateFullTextPaths(value);
                this.fullTextPathsInternal = value;
            }
        }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields.
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [System.Text.Json.Serialization.JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }

        /// <summary>
        /// Ensures that the specified full text paths in the policy are valid.
        /// </summary>
        private static void ValidateFullTextPaths(
            IEnumerable<FullTextPath> fullTextPaths)
        {
            foreach (FullTextPath item in fullTextPaths)
            {
                item.ValidateFullTextPath();
            }
        }
    }
}