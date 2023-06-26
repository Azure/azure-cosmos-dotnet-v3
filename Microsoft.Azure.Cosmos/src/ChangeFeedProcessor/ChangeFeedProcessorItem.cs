//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// An item representing a document change
    /// </summary>
    /// <typeparam name="T">The document type</typeparam>
    public sealed class ChangeFeedProcessorItem<T>
    {
        /// <summary>
        /// The current state of the document
        /// </summary>
        [JsonPropertyName("current")]
        public T Current { get; set; }

        /// <summary>
        /// The previous state of the document
        /// </summary>
        [JsonPropertyName("previous")]
        public T Previous { get; set; }

        /// <summary>
        /// The change metadata
        /// </summary>
        [JsonPropertyName("metadata")]
#if PREVIEW
        public
#else
        internal
#endif
            ChangeFeedMetadata Metadata { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public ChangeFeedProcessorItem()
        {
        }

        internal ChangeFeedProcessorItem(T current,
            T previous,
            ChangeFeedMetadata metadata)
        {
            this.Current = current;
            this.Previous = previous;
            this.Metadata = metadata;
        }
    }
}