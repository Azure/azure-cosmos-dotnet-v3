//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FullFidelity
{
    using Newtonsoft.Json;

    /// <summary>
    /// The metadata of full fidelity change feeds.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
        class Metadata
    {
        /// <summary>
        /// The conflict resolved timestamp.
        /// </summary>
        [JsonProperty(PropertyName = "crts", NullValueHandling = NullValueHandling.Ignore)]
        public long CRTS { get; set; }

        /// <summary>
        /// The log sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "lsn", NullValueHandling = NullValueHandling.Ignore)]
        public long LSN { get; set; }

        /// <summary>
        /// The operation type.
        /// </summary>
        [JsonProperty(PropertyName = "operationType")]
        public OperationType OperationType { get; set; }

        /// <summary>
        /// The previous image log sequence number.
        /// </summary>
        [JsonProperty(PropertyName = "previousImageLSN", NullValueHandling = NullValueHandling.Ignore)]
        public long PreviousImageLSN { get; set; }
    }
}
