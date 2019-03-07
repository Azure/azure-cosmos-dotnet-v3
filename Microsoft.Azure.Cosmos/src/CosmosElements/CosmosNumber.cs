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

        public abstract bool IsFloatingPoint
        {
            get;
        }

        public abstract double? AsFloatingPoint();

        public abstract long? AsInteger();
    }
}
