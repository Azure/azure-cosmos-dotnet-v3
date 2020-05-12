//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 107

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosInt64 : CosmosNumber
    {
        private sealed class EagerCosmosInt64 : CosmosInt64
        {
            private readonly long number;

            public EagerCosmosInt64(long number)
            {
                this.number = number;
            }

            public override long GetValue()
            {
                return this.number;
            }
        }
    }
}
