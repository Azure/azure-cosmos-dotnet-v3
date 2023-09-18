//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLTupleType : QLType
    {
        public QLTupleType(IReadOnlyList<QLType> types)
            : base(QLTypeKind.Tuple)
        {
            this.Types = types;
        }

        public IReadOnlyList<QLType> Types { get; }
    }

}