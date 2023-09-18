//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLBaseType : QLType
    {
        public QLBaseType(bool excludesUndefined)
            : base(QLTypeKind.Base)
        {
            this.ExcludesUndefined = excludesUndefined;
        }

        public bool ExcludesUndefined { get; }
    }
}