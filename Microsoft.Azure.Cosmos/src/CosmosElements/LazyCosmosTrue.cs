namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class LazyCosmosTrue : CosmosTrue, ILazyCosmosElement
    {
        public static readonly LazyCosmosTrue Singleton = new LazyCosmosTrue();

        public void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null");
            }

            jsonWriter.WriteBoolValue(true);
        }
    }
}
