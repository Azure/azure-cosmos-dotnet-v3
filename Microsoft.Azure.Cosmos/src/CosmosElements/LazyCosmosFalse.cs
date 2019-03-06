namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class LazyCosmosFalse : CosmosFalse, ILazyCosmosElement
    {
        public static readonly LazyCosmosFalse Singleton = new LazyCosmosFalse();

        public void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null");
            }

            jsonWriter.WriteBoolValue(false);
        }
    }
}
