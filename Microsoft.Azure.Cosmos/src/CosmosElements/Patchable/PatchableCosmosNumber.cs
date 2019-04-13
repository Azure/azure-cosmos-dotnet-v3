namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;

    internal sealed class PatchableCosmosNumber : PatchableCosmosElement
    {
        private readonly CosmosNumber cosmosNumber;
        private PatchableCosmosNumber(CosmosNumber cosmosNumber)
            : base(PatchableCosmosElementType.Number)
        {
            if (cosmosNumber == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosNumber)}");
            }

            this.cosmosNumber = cosmosNumber;
        }

        public override CosmosElement ToCosmosElement()
        {
            return this.cosmosNumber;
        }

        public static PatchableCosmosNumber Create(CosmosNumber cosmosNumber)
        {
            return new PatchableCosmosNumber(cosmosNumber);
        }
    }
}
