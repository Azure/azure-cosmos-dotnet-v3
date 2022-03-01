//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FullFidelity
{
    using Newtonsoft.Json;

    /// <summary>
    /// The resource of full fidelity change feeds.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif  
        class ChangeFeedItem<T>
    {
        /// <summary>
        /// The full fidelity change feed current item.
        /// </summary>
        [JsonProperty(PropertyName = "current")]
        public T Current { get; set; }

        /// <summary>
        /// The full fidelity change feed metadata.
        /// </summary>
        [JsonProperty(PropertyName = "metadata", NullValueHandling = NullValueHandling.Ignore)]
        public Metadata Metadata { get; set; }

        /// <summary>
        /// The full fidelity change feed previous item.
        /// </summary>
        [JsonProperty(PropertyName = "previous", NullValueHandling = NullValueHandling.Ignore)]
        public T Previous { get; set; }
    }
}
