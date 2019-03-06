namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract class CosmosElement
    {
        protected CosmosElement(CosmosElementType cosmosItemType)
        {
            this.Type = cosmosItemType;
        }

        public CosmosElementType Type
        {
            get;
        }
    }
}
