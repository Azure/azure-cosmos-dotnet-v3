//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// The typed response that contains the current, previous, and metadata change feed resource when <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.FullFidelity"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif  
        class ChangeFeedItemChanges<T>
    {
        /// <summary>
        /// New instance of ItemChanges that contain change feed resource along with metadata.
        /// </summary>
        /// <param name="current"></param>
        /// <param name="changeFeedMetadata"></param>
        /// <param name="previous"></param>
        public ChangeFeedItemChanges(T current, ChangeFeedMetadata changeFeedMetadata, T previous)
        {
            this.Current = current;
            this.Metadata = changeFeedMetadata;
            this.Previous = previous;
        }

        /// <summary>
        /// The full fidelity change feed current item.
        /// </summary>
        [JsonProperty(PropertyName = "current")]
        public T Current { get; set; }

        /// <summary>
        /// The full fidelity change feed metadata.
        /// </summary>
        [JsonProperty(PropertyName = "metadata", NullValueHandling = NullValueHandling.Ignore)]
        public ChangeFeedMetadata Metadata { get; set; }

        /// <summary>
        /// The full fidelity change feed previous item.
        /// </summary>
        [JsonProperty(PropertyName = "previous", NullValueHandling = NullValueHandling.Ignore)]
        public T Previous { get; set; }
    }
}
