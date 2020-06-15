//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    internal sealed class PatchOperation<T> : PatchOperation
    {
        private const string ValuePropertyName = "value";

        [JsonProperty(PropertyName = ValuePropertyName, NullValueHandling = NullValueHandling.Ignore)]
        public T Value { get; }

        public PatchOperation(
            PatchOperationType operationType,
            string path,
            T value,
            string from = null)
            : base(operationType, path, from)
        {
            this.Value = value;
        }
    }
}
