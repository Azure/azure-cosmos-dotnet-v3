namespace Microsoft.Azure.Cosmos.CosmosElements
{
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

        public override bool IsDouble
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

        public override double GetValueAsDouble()
        {
            return Number64.ToDouble(this.number);
        }

        public override long GetValueAsLong()
        {
            return Number64.ToLong(this.number);
        }
    }
}
