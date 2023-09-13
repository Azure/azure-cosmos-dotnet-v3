//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal abstract class ClientQLType
    {
        public ClientQLType(ClientQLTypeKind kind)
        {
            this.Kind = kind;
        }

        public ClientQLTypeKind Kind { get; }
    }
}