//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

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
    abstract partial class CosmosString : CosmosElement, IEquatable<CosmosString>, IComparable<CosmosString>
    {
        private sealed class EagerCosmosString : CosmosString
        {
            public EagerCosmosString(UtfAnyString value)
            {
                this.Value = value;
            }

            public override UtfAnyString Value { get; }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                jsonWriter.WriteStringValue(this.Value);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}