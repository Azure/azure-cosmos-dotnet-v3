namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class LazyCosmosNull : CosmosNull, ILazyCosmosElement
    {
        public static readonly LazyCosmosNull Singleton = new LazyCosmosNull();

        public void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null");
            }

            jsonWriter.WriteNullValue();
        }
    }
}
