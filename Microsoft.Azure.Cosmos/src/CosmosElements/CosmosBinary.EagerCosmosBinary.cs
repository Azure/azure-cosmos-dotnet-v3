//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosBinary : CosmosElement
    {
        private sealed class EagerCosmosBinary : CosmosBinary
        {
            public EagerCosmosBinary(ReadOnlyMemory<byte> value)
            {
                this.Value = value;
            }

            public override ReadOnlyMemory<byte> Value { get; }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteBinaryValue(this.Value.Span);
            }
        }
    }
}