namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosNumber : CosmosElement
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

        public static CosmosNumber Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosNumber(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosNumber Create(Number64 number)
        {
            return new EagerCosmosNumber(number);
        }
    }
}
