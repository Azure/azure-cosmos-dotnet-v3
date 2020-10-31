//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

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
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>, IEquatable<CosmosArray>, IComparable<CosmosArray>
    {
        private sealed class EagerCosmosArray : CosmosArray
        {
            private readonly List<CosmosElement> cosmosElements;

            public EagerCosmosArray(IEnumerable<CosmosElement> elements)
            {
                this.cosmosElements = new List<CosmosElement>(elements);
            }

            public override int Count => this.cosmosElements.Count;

            public override CosmosElement this[int index] => this.cosmosElements[index];

            public override IEnumerator<CosmosElement> GetEnumerator() => this.cosmosElements.GetEnumerator();

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                jsonWriter.WriteArrayStart();

                foreach (CosmosElement arrayItem in this)
                {
                    arrayItem.WriteTo(jsonWriter);
                }

                jsonWriter.WriteArrayEnd();
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}