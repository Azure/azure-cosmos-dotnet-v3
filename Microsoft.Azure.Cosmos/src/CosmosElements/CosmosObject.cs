//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>
    {
        protected CosmosObject()
            : base(CosmosElementType.Object)
        {
        }

        public abstract IEnumerable<string> Keys
        {
            get;
        }

        public abstract IEnumerable<CosmosElement> Values
        {
            get;
        }

        public abstract int Count
        {
            get;
        }

        public abstract CosmosElement this[string key]
        {
            get;
        }

        public static CosmosObject Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosObject(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosObject Create(IDictionary<string, CosmosElement> dictionary)
        {
            return new EagerCosmosObject(dictionary);
        }

        public abstract bool ContainsKey(string key);

        public abstract bool TryGetValue(string key, out CosmosElement value);

        public abstract IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
