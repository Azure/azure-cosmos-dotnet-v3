//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    internal sealed class AddPatchOperation<T> : PatchOperation
    {
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Value)]
        public T Value { get; }

        public AddPatchOperation(
            string path,
            T value)
            : base(PatchOperationType.Add, path)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override string SerializeValueParameter(
            CosmosSerializer cosmosSerializer)
        {
            return this.SerializeValue(
                cosmosSerializer,
                this.Value);
        }
    }
}
