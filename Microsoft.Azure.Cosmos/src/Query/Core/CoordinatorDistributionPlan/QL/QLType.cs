//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal abstract class QLType
    {
        public QLType(QLTypeKind kind)
        {
            this.Kind = kind;
        }

        public QLTypeKind Kind { get; }
    }
}