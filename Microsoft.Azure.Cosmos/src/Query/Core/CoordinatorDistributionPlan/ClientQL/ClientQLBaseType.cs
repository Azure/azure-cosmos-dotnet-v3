//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBaseType : ClientQLType
    {
        public ClientQLBaseType(bool excludesUndefined)
            : base(ClientQLTypeKind.Base)
        {
            this.ExcludesUndefined = excludesUndefined;
        }

        public bool ExcludesUndefined { get; }
    }
}