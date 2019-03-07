namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class CosmosNull : CosmosElement
    {
        public static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base(CosmosElementType.Null)
        {
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteNullValue();
        }
    }
}
