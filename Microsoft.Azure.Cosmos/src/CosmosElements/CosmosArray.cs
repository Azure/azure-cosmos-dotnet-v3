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
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>
    {
        protected CosmosArray()
            : base(CosmosElementType.Array)
        {
        }

        public abstract int Count
        {
            get;
        }

        public abstract CosmosElement this[int index]
        {
            get;
        }

        public static CosmosArray Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosArray(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosArray Create(IEnumerable<CosmosElement> cosmosElements)
        {
            return new EagerCosmosArray(cosmosElements);
        }

        public abstract IEnumerator<CosmosElement> GetEnumerator();

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
