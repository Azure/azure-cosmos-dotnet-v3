namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.Collections;
    using System.Collections.Generic;

    internal abstract class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>
    {
        protected CosmosArray()
            : base (CosmosElementType.Array)
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
    }
}
