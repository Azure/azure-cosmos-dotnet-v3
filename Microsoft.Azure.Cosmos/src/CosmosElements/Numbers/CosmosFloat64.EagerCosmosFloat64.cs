//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 101

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
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
    abstract partial class CosmosFloat64 : CosmosNumber, IEquatable<CosmosFloat64>, IComparable<CosmosFloat64>
    {
        private sealed class EagerCosmosFloat64 : CosmosFloat64
        {
            private readonly double number;

            public EagerCosmosFloat64(double number)
            {
                this.number = number;
            }

            public override double GetValue()
            {
                return this.number;
            }
        }
    }
}
