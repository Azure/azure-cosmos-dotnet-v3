namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;

    internal abstract class PatchableCosmosElement
    {
        public PatchableCosmosElement(PatchableCosmosElementType type)
        {
            this.Type = type;
        }

        public PatchableCosmosElementType Type
        {
            get;
        }

        public abstract CosmosElement ToCosmosElement();
    }
}
