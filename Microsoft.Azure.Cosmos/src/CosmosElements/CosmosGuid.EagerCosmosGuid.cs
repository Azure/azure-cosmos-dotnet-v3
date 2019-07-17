//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;

#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosGuid : CosmosElement
#else
    internal abstract partial class CosmosGuid : CosmosElement
#endif
    {
        private sealed class EagerCosmosGuid : CosmosGuid
        {
            public EagerCosmosGuid(Guid value)
            {
                this.Value = value;
            }

            public override Guid Value
            {
                get;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}