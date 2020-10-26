//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>, IEquatable<CosmosObject>, IComparable<CosmosObject>
    {
        private const uint HashSeed = 1275696788;
        private const uint NameHashSeed = 263659187;

        protected CosmosObject()
            : base()
        {
        }

        public abstract KeyCollection Keys { get; }

        IEnumerable<string> IReadOnlyDictionary<string, CosmosElement>.Keys => this.Keys;

        public abstract ValueCollection Values { get; }

        IEnumerable<CosmosElement> IReadOnlyDictionary<string, CosmosElement>.Values => this.Values;

        public abstract int Count { get; }

        public abstract CosmosElement this[string key] { get; }

        public abstract bool ContainsKey(string key);

        public abstract bool TryGetValue(string key, out CosmosElement value);

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            return cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            return cosmosElementVisitor.Visit(this, input);
        }

        public bool TryGetValue<TCosmosElement>(string key, out TCosmosElement typedCosmosElement)
            where TCosmosElement : CosmosElement
        {
            if (!this.TryGetValue(key, out CosmosElement cosmosElement))
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                typedCosmosElement = default;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                return false;
            }

            if (!(cosmosElement is TCosmosElement tCosmosElement))
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                typedCosmosElement = default;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                return false;
            }

            typedCosmosElement = tCosmosElement;
            return true;
        }

        public abstract Enumerator GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        IEnumerator<KeyValuePair<string, CosmosElement>> IEnumerable<KeyValuePair<string, CosmosElement>>.GetEnumerator() => this.GetEnumerator();

        public override bool Equals(CosmosElement cosmosElement)
        {
            return cosmosElement is CosmosObject cosmosObject && this.Equals(cosmosObject);
        }

        public bool Equals(CosmosObject cosmosObject)
        {
            if (this.Count != cosmosObject.Count)
            {
                return false;
            }

            // Order of properties does not mattter
            foreach (KeyValuePair<string, CosmosElement> kvp in this)
            {
                string propertyName = kvp.Key;
                CosmosElement propertyValue = kvp.Value;

                if (!cosmosObject.TryGetValue(propertyName, out CosmosElement otherPropertyValue))
                {
                    return false;
                }

                if (propertyValue != otherPropertyValue)
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            uint hash = HashSeed;
            foreach (KeyValuePair<string, CosmosElement> kvp in this)
            {
                uint nameHash = MurmurHash3.Hash32(kvp.Key, NameHashSeed);
                uint valueHash = MurmurHash3.Hash32(kvp.Value.GetHashCode(), nameHash);

                //// xor is symmetric meaning that a ^ b = b ^ a
                //// Which is great since now we can add the property hashes to the intermediate hash
                //// in any order and get the same result, which upholds our definition of equality.
                //// Note that we don't have to worry about a ^ a = 0 = b ^ b for duplicate property values,
                //// since the hash of property values are seeded with the hash of property names,
                //// which are unique within an object.

                hash ^= valueHash;
            }

            return (int)hash;
        }

        public int CompareTo(CosmosObject cosmosObject)
        {
            UInt128 hash1 = DistinctHash.GetHash(this);
            UInt128 hash2 = DistinctHash.GetHash(cosmosObject);
            return hash1.CompareTo(hash2);
        }

        public static CosmosObject Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosObject(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosObject Create(IReadOnlyDictionary<string, CosmosElement> dictionary)
        {
            return new EagerCosmosObject(dictionary);
        }

        public static new CosmosObject CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosObject>(buffer);
        }

        public static new CosmosObject Parse(string json)
        {
            return CosmosElement.Parse<CosmosObject>(json);
        }

        public static bool TryCreateFromBuffer(
            ReadOnlyMemory<byte> buffer,
            out CosmosObject cosmosObject)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosObject>(buffer, out cosmosObject);
        }

        public static bool TryParse(
            string json,
            out CosmosObject cosmosObject)
        {
            return CosmosElement.TryParse<CosmosObject>(json, out cosmosObject);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosObject> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosObject>(buffer);
            }

            public static TryCatch<CosmosObject> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosObject>(json);
            }
        }

        public struct Enumerator : IEnumerator<KeyValuePair<string, CosmosElement>>
        {
            private Dictionary<string, CosmosElement>.Enumerator innerEnumerator;

            internal Enumerator(Dictionary<string, CosmosElement>.Enumerator innerEnumerator)
            {
                this.innerEnumerator = innerEnumerator;
            }

            public KeyValuePair<string, CosmosElement> Current => this.innerEnumerator.Current;

            object IEnumerator.Current => this.innerEnumerator.Current;

            public void Dispose() => this.innerEnumerator.Dispose();

            public bool MoveNext() => this.innerEnumerator.MoveNext();

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        public struct KeyCollection : IEnumerable<string>
        {
            private Dictionary<string, CosmosElement>.KeyCollection innerCollection;

            internal KeyCollection(Dictionary<string, CosmosElement>.KeyCollection innerCollection)
            {
                this.innerCollection = innerCollection;
            }

            public Enumerator GetEnumerator() => new Enumerator(this.innerCollection.GetEnumerator());

            IEnumerator<string> IEnumerable<string>.GetEnumerator() => this.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

            public struct Enumerator : IEnumerator<string>
            {
                private Dictionary<string, CosmosElement>.KeyCollection.Enumerator innerEnumerator;

                internal Enumerator(Dictionary<string, CosmosElement>.KeyCollection.Enumerator innerEnumerator)
                {
                    this.innerEnumerator = innerEnumerator;
                }

                public string Current => this.innerEnumerator.Current;

                object IEnumerator.Current => this.innerEnumerator.Current;

                public void Dispose() => this.innerEnumerator.Dispose();

                public bool MoveNext()
                {
                    return this.innerEnumerator.MoveNext();
                }

                public void Reset()
                {
                    throw new NotImplementedException();
                }
            }
        }

        public struct ValueCollection : IEnumerable<CosmosElement>
        {
            private Dictionary<string, CosmosElement>.ValueCollection innerCollection;

            internal ValueCollection(Dictionary<string, CosmosElement>.ValueCollection innerCollection)
            {
                this.innerCollection = innerCollection;
            }

            public Enumerator GetEnumerator() => new Enumerator(this.innerCollection.GetEnumerator());

            IEnumerator<CosmosElement> IEnumerable<CosmosElement>.GetEnumerator() => this.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

            public struct Enumerator : IEnumerator<CosmosElement>
            {
                private Dictionary<string, CosmosElement>.ValueCollection.Enumerator innerEnumerator;

                internal Enumerator(Dictionary<string, CosmosElement>.ValueCollection.Enumerator innerEnumerator)
                {
                    this.innerEnumerator = innerEnumerator;
                }

                public CosmosElement Current => this.innerEnumerator.Current;

                object IEnumerator.Current => this.innerEnumerator.Current;

                public void Dispose() => this.innerEnumerator.Dispose();

                public bool MoveNext() => this.innerEnumerator.MoveNext();

                public void Reset()
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
