namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal sealed class CosmosNull : CosmosElement
    {
        public static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base(CosmosElementType.Null)
        {
        }
    }
}
