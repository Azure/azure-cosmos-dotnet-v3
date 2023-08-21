//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLEnumType : ClientQLType
    {
        public ClientQLEnumType(ClientQLType itemType)
            : base(ClientQLTypeKind.Enum)
        {
            this.ItemType = itemType;
        }

        public ClientQLType ItemType { get; }
    }
}