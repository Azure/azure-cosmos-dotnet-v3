namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal abstract class CosmosBoolean : CosmosElement
    {
        protected CosmosBoolean()
            : base(CosmosElementType.Boolean)
        {
        }

        public abstract bool Value
        {
            get;
        }
    }
}
