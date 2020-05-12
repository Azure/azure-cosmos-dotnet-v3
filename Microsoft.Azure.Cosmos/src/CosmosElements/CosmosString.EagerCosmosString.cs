//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosString : CosmosElement
    {
        private sealed class EagerCosmosString : CosmosString
        {
            public EagerCosmosString(string value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException($"{nameof(value)}");
                }

                this.Value = value;
            }

            public override string Value { get; }

            public override bool TryGetBufferedValue(out Utf8Memory bufferedValue)
            {
                // Eager string only has the materialized value, so this method will always return false.
                bufferedValue = default;
                return false;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteStringValue(this.Value);
            }
        }
    }
}