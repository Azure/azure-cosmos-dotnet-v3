namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.Collections;
    using System.Collections.Generic;

    internal abstract class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>
    {
        protected CosmosObject()
            : base (CosmosElementType.Object)
        {
        }

        public abstract CosmosElement this[string key]
        {
            get;
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

        public abstract bool ContainsKey(string key);

        public abstract bool TryGetValue(string key, out CosmosElement value);

        public abstract IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
