//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Describes the source document paths and the embedding service that the Azure Cosmos DB
    /// service should use to generate the vector value for an <see cref="Embedding"/>.
    /// </summary>
    /// <remarks>
    /// When present on an <see cref="Embedding"/>, this block tells the SDK (and the Cosmos
    /// DB embedding provider) where and how to call the embedding service for the vector
    /// path in question.
    /// </remarks>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class EmbeddingSource : IEquatable<EmbeddingSource>
    {
        /// <summary>
        /// Gets or sets the list of document paths whose values are concatenated and sent to
        /// the embedding service to generate the vector.
        /// </summary>
        [JsonProperty(PropertyName = "sourcePaths")]
        public Collection<string> SourcePaths { get; set; }

        /// <summary>
        /// Gets or sets the deployment name of the embedding model on the embedding service.
        /// </summary>
        [JsonProperty(PropertyName = "deploymentName")]
        public string DeploymentName { get; set; }

        /// <summary>
        /// Gets or sets the name of the embedding model.
        /// </summary>
        [JsonProperty(PropertyName = "modelName")]
        public string ModelName { get; set; }

        /// <summary>
        /// Gets or sets the endpoint of the embedding service.
        /// </summary>
        [JsonProperty(PropertyName = "endpoint")]
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Cosmos.EmbeddingAuthType"/> used to authenticate to the
        /// embedding service.
        /// </summary>
        [JsonProperty(PropertyName = "authType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public EmbeddingAuthType AuthType { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields.
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        /// <inheritdoc/>
        public bool Equals(EmbeddingSource that)
        {
            if (that is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, that))
            {
                return true;
            }

            return ((this.SourcePaths == null && that.SourcePaths == null) ||
                    (this.SourcePaths != null && that.SourcePaths != null && Enumerable.SequenceEqual(this.SourcePaths, that.SourcePaths)))
                && this.AuthType == that.AuthType
                && this.DeploymentName == that.DeploymentName
                && this.Endpoint == that.Endpoint
                && this.ModelName == that.ModelName;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as EmbeddingSource);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1265339359;

            if (this.SourcePaths != null)
            {
                foreach (string sourcePath in this.SourcePaths)
                {
                    hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(sourcePath);
                }
            }

            hashCode = (hashCode * -1521134295) + this.AuthType.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.DeploymentName);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Endpoint);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.ModelName);
            return hashCode;
        }
    }
}
