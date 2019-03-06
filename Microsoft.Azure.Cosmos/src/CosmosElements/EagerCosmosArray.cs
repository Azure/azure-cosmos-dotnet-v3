namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;

    internal sealed class EagerCosmosArray : CosmosArray
    {
        private readonly List<CosmosElement> cosmosElements;

        public EagerCosmosArray(IEnumerable<CosmosElement> elements)
        {
            if (elements == null)
            {
                throw new ArgumentNullException($"{nameof(elements)} must not be null.");
            }

            foreach (CosmosElement element in elements)
            {
                if (element == null)
                {
                    throw new ArgumentException($"{nameof(elements)} must not have null items.");
                }
            }

            this.cosmosElements = new List<CosmosElement>(elements);
        }

        public override CosmosElement this[int index]
        {
            get
            {
                return this.cosmosElements[index];
            }
        }

        public override int Count => this.cosmosElements.Count;

        public override IEnumerator<CosmosElement> GetEnumerator() => this.cosmosElements.GetEnumerator();
    }
}
