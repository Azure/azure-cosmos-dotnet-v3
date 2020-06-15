//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal class PatchOperation
    {
        private static class PropertyNames
        {
            public const string OperationType = "op";
            public const string Path = "path";
            public const string From = "from";
        }

        [JsonProperty(PropertyName = PropertyNames.OperationType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PatchOperationType OperationType { get; }

        [JsonProperty(PropertyName = PropertyNames.Path)]
        public string Path { get; }

        [JsonProperty(PropertyName = PropertyNames.From, NullValueHandling = NullValueHandling.Ignore)]
        public string From { get; }

        public PatchOperation(
            PatchOperationType operationType,
            string path,
            string from = null)
        {
            this.OperationType = operationType;
            this.Path = path;
            this.From = from;
        }
    }
}
