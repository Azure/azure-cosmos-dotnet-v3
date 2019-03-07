namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Newtonsoft.Json;

    internal sealed class EagerCosmosObject : CosmosObject
    {
        private readonly Dictionary<string, CosmosElement> dictionary;

        public EagerCosmosObject(IDictionary<string, CosmosElement> dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException($"{nameof(dictionary)} must not be null.");
            }

            this.dictionary = new Dictionary<string, CosmosElement>(dictionary);
        }

        public override CosmosElement this[string key] => this.dictionary[key];

        public override IEnumerable<string> Keys => this.dictionary.Keys;

        public override IEnumerable<CosmosElement> Values => this.dictionary.Values;

        public override int Count => this.dictionary.Count;

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
    }
}