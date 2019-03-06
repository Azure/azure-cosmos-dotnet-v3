namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract class CosmosString : CosmosElement
    {
        protected CosmosString()
            : base (CosmosElementType.String)
        {
        }

        public abstract string Value
        {
            get;
        }
    }
}
