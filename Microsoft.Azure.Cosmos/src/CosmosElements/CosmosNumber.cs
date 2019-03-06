namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract class CosmosNumber : CosmosElement
    {
        protected CosmosNumber()
            : base (CosmosElementType.Number)
        {
        }

        public abstract bool IsInteger
        {
            get;
        }

        public abstract bool IsDouble
        {
            get;
        }

        public abstract double GetValueAsDouble();

        public abstract long GetValueAsLong();
    }
}
