//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    internal sealed class ReplacePatchOperation<T> : PatchOperation
    {
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Value)]
        public T Value { get; }

        public ReplacePatchOperation(
            string path,
            T value)
            : base(PatchOperationType.Replace, path)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
