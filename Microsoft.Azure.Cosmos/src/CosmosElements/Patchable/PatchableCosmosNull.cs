namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    internal sealed class PatchableCosmosNull : PatchableCosmosElement
    {
        private static readonly CosmosNull CosmosNull = CosmosNull.Create();
        private static readonly PatchableCosmosNull Singleton = new PatchableCosmosNull();

        private PatchableCosmosNull()
            : base(PatchableCosmosElementType.Null)
        {
            // All the work is done in the base constructor.
        }

        public static PatchableCosmosNull Create()
        {
            return Singleton;
        }

        public override CosmosElement ToCosmosElement()
        {
            return CosmosNull;
        }
    }
}
