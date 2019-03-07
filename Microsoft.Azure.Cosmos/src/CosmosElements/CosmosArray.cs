namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>
    {
        protected CosmosArray()
            : base(CosmosElementType.Array)
        {
        }

        public abstract CosmosElement this[int index]
        {
            get;
        }

        public abstract int Count
        {
            get;
        }

        public abstract IEnumerator<CosmosElement> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
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
    }
}
