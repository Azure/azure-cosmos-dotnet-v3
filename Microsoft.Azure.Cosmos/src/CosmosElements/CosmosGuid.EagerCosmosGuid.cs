//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosGuid : CosmosElement, IEquatable<CosmosGuid>, IComparable<CosmosGuid>
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
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}