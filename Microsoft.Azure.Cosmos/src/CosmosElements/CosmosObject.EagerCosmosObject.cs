//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>, IEquatable<CosmosObject>, IComparable<CosmosObject>
    {
        private sealed class EagerCosmosObject : CosmosObject
        {
            private readonly Dictionary<string, CosmosElement> dictionary;

            public EagerCosmosObject(IReadOnlyDictionary<string, CosmosElement> dictionary)
            {
                this.dictionary = new Dictionary<string, CosmosElement>(dictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            }

            public override CosmosElement this[string key] => this.dictionary[key];

            public override int Count => this.dictionary.Count;

            public override KeyCollection Keys => new KeyCollection(this.dictionary.Keys);

            public override ValueCollection Values => new ValueCollection(this.dictionary.Values);

            public override bool ContainsKey(string key) => this.dictionary.ContainsKey(key);

            public override Enumerator GetEnumerator() => new Enumerator(this.dictionary.GetEnumerator());

            public override bool TryGetValue(string key, out CosmosElement value) => this.dictionary.TryGetValue(key, out value);

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                jsonWriter.WriteObjectStart();

                foreach (KeyValuePair<string, CosmosElement> kvp in this.dictionary)
                {
                    jsonWriter.WriteFieldName(kvp.Key);
                    kvp.Value.WriteTo(jsonWriter);
                }

                jsonWriter.WriteObjectEnd();
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}