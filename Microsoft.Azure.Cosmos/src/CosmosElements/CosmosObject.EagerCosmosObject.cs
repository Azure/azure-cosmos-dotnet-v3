//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
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
            private readonly List<KeyValuePair<string, CosmosElement>> properties;

            public EagerCosmosObject(IReadOnlyList<KeyValuePair<string, CosmosElement>> properties)
            {
                if (properties == null)
                {
                    throw new ArgumentNullException(nameof(properties));
                }

                this.properties = new List<KeyValuePair<string, CosmosElement>>(properties);
            }

            public override CosmosElement this[string key]
            {
                get
                {
                    if (!this.TryGetValue(key, out CosmosElement value))
                    {
                        throw new KeyNotFoundException($"Failed to find key: {key}");
                    }

                    return value;
                }
            }

            public override IEnumerable<string> Keys => this.properties.Select(kvp => kvp.Key);

            public override IEnumerable<CosmosElement> Values => this.properties.Select(kvp => kvp.Value);

            public override int Count => this.properties.Count;

            public override bool ContainsKey(string key)
            {
                return this.TryGetValue(key, out CosmosElement unused);
            }

            public override IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator()
            {
                return this.properties.GetEnumerator();
            }

            public override bool TryGetValue(string key, out CosmosElement value)
            {
                foreach (KeyValuePair<string, CosmosElement> property in this.properties)
                {
                    if (property.Key == key)
                    {
                        value = property.Value;
                        return true;
                    }
                }

                value = null;
                return false;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteObjectStart();

                foreach (KeyValuePair<string, CosmosElement> kvp in this.properties)
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