//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleType : ClientQLType
    {
        public ClientQLTupleType(IReadOnlyList<ClientQLType> types)
            : base(ClientQLTypeKind.Tuple)
        {
            this.Types = types;
        }

        public IReadOnlyList<ClientQLType> Types { get; }
    }

}