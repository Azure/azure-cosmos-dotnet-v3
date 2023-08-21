//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLAggregateSignature
    {
        public ClientQLAggregateSignature(ClientQLTypeKind itemType, ClientQLTypeKind resultType)
        {
            this.ItemType = itemType;
            this.ResultType = resultType;
        }

        public ClientQLTypeKind ItemType { get; }
        
        public ClientQLTypeKind ResultType { get; }
    }
}