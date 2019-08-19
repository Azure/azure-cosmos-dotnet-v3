//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal abstract class BaseStatistics
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string userAgent { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string connectivityMode { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Tuple<string, TimeSpan>> customHandlerLatency { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? deserializationLatency { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? serializationLatency { get; set; }
    }
}
