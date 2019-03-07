namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class EagerCosmosString : CosmosString
    {
        public EagerCosmosString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException($"{nameof(value)} must not be null.");
            }

            this.Value = value;
        }

        public override string Value
        {
            get;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteStringValue(this.Value);
        }
    }
}