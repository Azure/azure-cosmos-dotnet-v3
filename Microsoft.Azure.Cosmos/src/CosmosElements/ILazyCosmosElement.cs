namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.IO;
    using Microsoft.Azure.Cosmos.Json;

    internal interface ILazyCosmosElement
    {
        void WriteToWriter(IJsonWriter jsonWriter);
    }
}
