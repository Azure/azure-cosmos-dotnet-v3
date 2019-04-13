using Microsoft.Azure.Cosmos.Json;

namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    internal sealed class PatchableCosmosBoolean : PatchableCosmosElement
    {
        private static readonly PatchableCosmosBoolean True = new PatchableCosmosBoolean(CosmosBoolean.Create(true));
        private static readonly PatchableCosmosBoolean False = new PatchableCosmosBoolean(CosmosBoolean.Create(false));
        private readonly CosmosBoolean cosmosBoolean;
        private PatchableCosmosBoolean(CosmosBoolean cosmosBoolean)
            : base(PatchableCosmosElementType.Boolean)
        {
            // All the work is done in the base constructor.
            this.cosmosBoolean = cosmosBoolean;
        }

        public override CosmosElement ToCosmosElement()
        {
            return this.cosmosBoolean;
        }

        public static PatchableCosmosBoolean Create(CosmosBoolean cosmosBoolean)
        {
            return cosmosBoolean.Value ? PatchableCosmosBoolean.True : PatchableCosmosBoolean.False;
        }
    }
}
