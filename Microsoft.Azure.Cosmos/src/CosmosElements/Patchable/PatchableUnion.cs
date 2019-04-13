namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;

    internal abstract class PatchableUnion : PatchableCosmosElement
    {
        private PatchableUnion(PatchableCosmosElementType type)
            : base(type)
        {
        }

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

        public static implicit operator PatchableCosmosArray(PatchableUnion union)
        {
            return (PatchableCosmosArray)union.PatchableCosmosElement;
        }

        public static implicit operator PatchableCosmosBoolean(PatchableUnion union)
        {
            return (PatchableCosmosBoolean)union.PatchableCosmosElement;
        }

        public static implicit operator PatchableCosmosNull(PatchableUnion union)
        {
            return (PatchableCosmosNull)union.PatchableCosmosElement;
        }

        public static implicit operator PatchableCosmosNumber(PatchableUnion union)
        {
            return (PatchableCosmosNumber)union.PatchableCosmosElement;
        }

        public static implicit operator PatchableCosmosObject(PatchableUnion union)
        {
            return (PatchableCosmosObject)union.PatchableCosmosElement;
        }

        public static implicit operator PatchableCosmosString(PatchableUnion union)
        {
            return (PatchableCosmosString)union.PatchableCosmosElement;
        }

        public override CosmosElement ToCosmosElement()
        {
            return this.CosmosElement;
        }

        private sealed class PatchableUnionPatch : PatchableUnion
        {
            private readonly PatchableCosmosElement patchableCosmosElement;
            private readonly Lazy<CosmosElement> cosmosElementWrapper;

            public PatchableUnionPatch(PatchableCosmosElement patchableCosmosElement)
                : base(patchableCosmosElement.Type)
            {
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
                : base(ConvertType(cosmosElement.Type))
            {
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

            private static PatchableCosmosElementType ConvertType(
                CosmosElementType cosmosElementType)
            {
                PatchableCosmosElementType patchableCosmosElementType;
                switch (cosmosElementType)
                {
                    case CosmosElementType.Array:
                        patchableCosmosElementType = PatchableCosmosElementType.Array;
                        break;

                    case CosmosElementType.Boolean:
                        patchableCosmosElementType = PatchableCosmosElementType.Boolean;
                        break;

                    case CosmosElementType.Null:
                        patchableCosmosElementType = PatchableCosmosElementType.Null;
                        break;

                    case CosmosElementType.Number:
                        patchableCosmosElementType = PatchableCosmosElementType.Number;
                        break;

                    case CosmosElementType.Object:
                        patchableCosmosElementType = PatchableCosmosElementType.Object;
                        break;

                    case CosmosElementType.String:
                        patchableCosmosElementType = PatchableCosmosElementType.String;
                        break;

                    default:
                        throw new ArgumentException($"Unknown {nameof(CosmosElementType)}: {cosmosElementType}");
                }

                return patchableCosmosElementType;
            }
        }
    }
}
