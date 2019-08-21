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
        internal JsonSerializerSettings jsonSerializerSettings;

        public string userAgent { get; set; }

        public string connectivityMode { get; set; }

        public List<Tuple<string, TimeSpan>> customHandlerLatency { get; set; }

        public TimeSpan? deserializationLatency { get; set; }

        public TimeSpan? serializationLatency { get; set; }

        public BaseStatistics()
        {
            this.jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }
    }
}
