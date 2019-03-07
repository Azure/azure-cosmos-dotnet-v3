namespace Microsoft.Azure.Cosmos.CosmosElements
{
    internal sealed class CosmosBoolean : CosmosElement
    {
        public static readonly CosmosBoolean True = new CosmosBoolean(true);
        public static readonly CosmosBoolean False = new CosmosBoolean(false);

        private CosmosBoolean(bool value)
            : base(CosmosElementType.Boolean)
        {
            this.Value = value;
        }

        public bool Value
        {
            get;
        }
    }
}
