//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>, IEquatable<CosmosObject>
    {
        protected CosmosObject()
            : base(CosmosElementType.Object)
        {
        }

        public abstract IEnumerable<string> Keys { get; }

        public abstract IEnumerable<CosmosElement> Values { get; }

        public abstract int Count { get; }

        public abstract CosmosElement this[string key] { get; }

        public abstract bool ContainsKey(string key);

        public abstract bool TryGetValue(string key, out CosmosElement value);

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this, input);
        }

        public bool TryGetValue<TCosmosElement>(string key, out TCosmosElement typedCosmosElement)
            where TCosmosElement : CosmosElement
        {
            if (!this.TryGetValue(key, out CosmosElement cosmosElement))
            {
                typedCosmosElement = default;
                return false;
            }

            if (!(cosmosElement is TCosmosElement tCosmosElement))
            {
                typedCosmosElement = default;
                return false;
            }

            typedCosmosElement = tCosmosElement;
            return true;
        }

        public abstract IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public override bool Equals(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return false;
            }

            return this.Equals(cosmosObject);
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

        public static CosmosObject Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosObject(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosObject Create(IReadOnlyDictionary<string, CosmosElement> dictionary)
        {
            return new EagerCosmosObject(dictionary.ToList());
        }

        public static CosmosObject Create(IReadOnlyList<KeyValuePair<string, CosmosElement>> properties)
        {
            return new EagerCosmosObject(properties);
        }

        public static new CosmosObject CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosObject>(buffer);
        }

        public static new CosmosObject Parse(string json)
        {
            return CosmosElement.Parse<CosmosObject>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosObject cosmosObject)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosObject>(buffer, out cosmosObject);
        }

        public static bool TryParse(string json, out CosmosObject cosmosObject)
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
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
