//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Newtonsoft.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>
    {
        private sealed class EagerCosmosObject : CosmosObject
        {
            private readonly Dictionary<string, CosmosElement> dictionary;

            public EagerCosmosObject(IDictionary<string, CosmosElement> dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException($"{nameof(dictionary)}");
                }

                this.dictionary = new Dictionary<string, CosmosElement>(dictionary);
            }

            public override IEnumerable<string> Keys => this.dictionary.Keys;

            public override IEnumerable<CosmosElement> Values => this.dictionary.Values;

            public override int Count => this.dictionary.Count;

            public override CosmosElement this[string key] => this.dictionary[key];

            public override bool ContainsKey(string key) => this.dictionary.ContainsKey(key);

            public override IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator() => this.dictionary.GetEnumerator();

            public override bool TryGetValue(string key, out CosmosElement value)
            {
                return this.dictionary.TryGetValue(key, out value);
            }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this.dictionary);
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteObjectStart();

                foreach (KeyValuePair<string, CosmosElement> kvp in this)
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