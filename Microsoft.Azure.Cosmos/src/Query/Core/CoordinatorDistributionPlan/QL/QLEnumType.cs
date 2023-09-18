//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLEnumType : QLType
    {
        public QLEnumType(QLType itemType)
            : base(QLTypeKind.Enum)
        {
            this.ItemType = itemType;
        }

        public QLType ItemType { get; }
    }
}