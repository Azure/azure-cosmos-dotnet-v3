//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal abstract class PatchOperation
    {
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.OperationType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PatchOperationType OperationType { get; }

        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Path)]
        public string Path { get; }

        public PatchOperation(
            PatchOperationType operationType,
            string path)
        {
            this.OperationType = operationType;
            this.Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentNullException(nameof(path))
                : path;
        }
    }
}
