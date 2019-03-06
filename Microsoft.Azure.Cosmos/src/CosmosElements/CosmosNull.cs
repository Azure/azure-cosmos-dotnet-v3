namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract class CosmosNull : CosmosElement
    {
        protected CosmosNull()
            : base(CosmosElementType.Null)
        {
        }
    }
}
