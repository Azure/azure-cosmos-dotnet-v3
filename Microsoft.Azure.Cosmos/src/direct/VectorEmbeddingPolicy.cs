//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the vector embedding policy configuration for specifying the vector embeddings on documents in the collection in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class VectorEmbeddingPolicy : JsonSerializable
    {
        private Collection<Embedding> embeddings;

        /// <summary>
        /// Initializes a new instance of the <see cref="VectorEmbeddingPolicy"/> class.
        /// </summary>
        public VectorEmbeddingPolicy()
        {

        }

        /// <summary>
        /// Gets a collection of <see cref="Embedding"/> that contains the vector embeddings of documents in collection in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.VectorEmbeddings)]
        public Collection<Embedding> Embeddings
        {
            get
            {
                if (this.embeddings == null)
                {
                    this.embeddings = base.GetObjectCollection<Embedding>("vectorEmbeddings");
                    if (this.embeddings == null)
                    {
                        this.embeddings = new Collection<Embedding>();
                    }
                }

                return this.embeddings;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(embeddings)));
                }

                this.embeddings = value;
                base.SetObjectCollection(Constants.Properties.VectorEmbeddings, this.embeddings);
            }
        }

        internal override void OnSave()
        {
            if (this.embeddings != null)
            {
                base.SetObjectCollection(Constants.Properties.VectorEmbeddings, this.embeddings);
            }
        }
    }
}
