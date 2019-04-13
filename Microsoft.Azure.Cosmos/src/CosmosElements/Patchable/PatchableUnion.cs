namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;

    internal abstract class PatchableUnion
    {
        public abstract PatchableCosmosElement PatchableCosmosElement
        {
            get;
        }

        public abstract CosmosElement CosmosElement
        {
            get;
        }

        public static PatchableUnion Create(PatchableCosmosElement patchableCosmosElement)
        {
            return new PatchableUnionPatch(patchableCosmosElement);
        }

        public static PatchableUnion Create(CosmosElement cosmosElement)
        {
            return new PatchableUnionCosmosElement(cosmosElement);
        }

        private sealed class PatchableUnionPatch : PatchableUnion
        {
            private readonly PatchableCosmosElement patchableCosmosElement;
            private readonly Lazy<CosmosElement> cosmosElementWrapper;

            public PatchableUnionPatch(PatchableCosmosElement patchableCosmosElement)
            {
                if(patchableCosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(patchableCosmosElement));
                }

                this.patchableCosmosElement = patchableCosmosElement;
                this.cosmosElementWrapper = new Lazy<CosmosElement>(() => 
                {
                    return patchableCosmosElement.ToCosmosElement();
                });
            }

            public override PatchableCosmosElement PatchableCosmosElement => this.patchableCosmosElement;

            public override CosmosElement CosmosElement => this.cosmosElementWrapper.Value;
        }

        private sealed class PatchableUnionCosmosElement : PatchableUnion
        {
            private readonly Lazy<PatchableCosmosElement> patchableCosmosElement;
            private CosmosElement cosmosElement;

            public PatchableUnionCosmosElement(CosmosElement cosmosElement)
            {
                if (cosmosElement == null)
                {
                    throw new ArgumentNullException(nameof(cosmosElement));
                }

                this.patchableCosmosElement = new Lazy<PatchableCosmosElement>(() =>
                {
                    return this.cosmosElement.ToPatchable();
                });

                this.cosmosElement = cosmosElement;
            }

            public override PatchableCosmosElement PatchableCosmosElement
            {
                get
                {
                    PatchableCosmosElement patchableCosmosElement = this.patchableCosmosElement.Value;
                    this.cosmosElement = patchableCosmosElement.ToCosmosElement();
                    return patchableCosmosElement;
                }
            }

            public override CosmosElement CosmosElement
            {
                get
                {
                    return this.cosmosElement;
                }
            }
        }
    }
}
