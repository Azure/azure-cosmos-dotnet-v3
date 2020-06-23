//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    internal sealed class SetPatchOperation<T> : PatchOperation
    {
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Value)]
        public T Value { get; }

        public SetPatchOperation(
            string path,
            T value)
            : base(PatchOperationType.Set, path)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
