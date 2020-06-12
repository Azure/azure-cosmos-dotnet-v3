//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Patch
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal sealed class PatchOperation
    {
        private const string operationTypePropertyName = "op";
        private const string pathPropertyName = "path";
        private const string fromPropertyName = "from";
        private const string valuePropertyName = "value";

        [JsonProperty(PropertyName = operationTypePropertyName)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PatchOperationType operationType { get; }

        [JsonProperty(PropertyName = pathPropertyName)]
        public string path { get; }

        [JsonProperty(PropertyName = fromPropertyName, NullValueHandling = NullValueHandling.Ignore)]
        public string from { get; }

        [JsonProperty(PropertyName = valuePropertyName, NullValueHandling = NullValueHandling.Ignore)]
        public object value { get; }

        public PatchOperation(
            PatchOperationType operationType,
            string path,
            object value = null,
            string from = null)
        {
            this.operationType = operationType;
            this.path = path;
            this.value = value;
            this.from = from;
        }
    }
}
