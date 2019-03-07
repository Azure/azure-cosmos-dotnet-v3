namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class EagerCosmosNumber : CosmosNumber
    {
        private readonly Number64 number;

        public EagerCosmosNumber(double number)
        {
            this.number = number;
        }

        public EagerCosmosNumber(long number)
        {
            this.number = number;
        }

        public override bool IsFloatingPoint
        {
            get
            {
                return this.number.IsDouble;
            }
        }

        public override bool IsInteger
        {
            get
            {
                return this.number.IsInteger;
            }
        }

        public override double? AsFloatingPoint()
        {
            return Number64.ToDouble(this.number);
        }

        public override long? AsInteger()
        {
            return Number64.ToLong(this.number);
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteNumberValue(this.AsFloatingPoint().Value);
        }
    }
}