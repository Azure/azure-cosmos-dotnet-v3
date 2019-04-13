namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;

    internal sealed class PatchableCosmosString : PatchableCosmosElement
    {
        private readonly CosmosString cosmosString;
        private PatchableCosmosString(CosmosString cosmosString)
            : base(PatchableCosmosElementType.String)
        {
            if (cosmosString == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosString)}");
            }

            this.cosmosString = cosmosString;
        }

        public override CosmosElement ToCosmosElement()
        {
            return this.cosmosString;
        }

        internal static PatchableCosmosElement Create(CosmosString cosmosString)
        {
            return new PatchableCosmosString(cosmosString);
        }
    }
}
