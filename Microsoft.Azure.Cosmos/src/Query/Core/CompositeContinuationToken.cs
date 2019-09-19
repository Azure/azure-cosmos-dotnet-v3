//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// A composite continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class CompositeContinuationToken
    {
        [JsonProperty("token")]
        public string Token
        {
            get;
            set;
        }

        [JsonProperty("range")]
        [JsonConverter(typeof(RangeJsonConverter))]
        public Range<string> Range
        {
            get;
            set;
        }

        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }
    }
}
