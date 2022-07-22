//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// A composite continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class CompositeContinuationToken
    {
        private static class PropertyNames
        {
            public const string Token = "token";
            public const string Range = "range";

            public const string Min = "min";
            public const string Max = "max";
        }

        [JsonProperty(PropertyNames.Token)]
        public string Token
        {
            get;
            set;
        }

        [JsonProperty(PropertyNames.Range)]
        [JsonConverter(typeof(RangeJsonConverter))]
        public Documents.Routing.Range<string> Range
        {
            get;
            set;
        }

        [JsonIgnore]
        public Range<string> PartitionRange => this.Range;

        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }
    }
}
