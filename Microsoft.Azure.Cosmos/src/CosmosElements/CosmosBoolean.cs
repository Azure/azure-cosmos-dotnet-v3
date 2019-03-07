namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    
    internal sealed class CosmosBoolean : CosmosElement
    {
        public static readonly CosmosBoolean True = new CosmosBoolean(true);
        public static readonly CosmosBoolean False = new CosmosBoolean(false);

        private CosmosBoolean(bool value)
            : base(CosmosElementType.Boolean)
        {
            this.Value = value;
        }

        public bool Value
        {
            get;
        }

        public override void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            if (this.Value)
            {
                jsonWriter.WriteBoolValue(true);
            }
            else
            {
                jsonWriter.WriteBoolValue(false);
            }
        }
    }
}
