//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLTupleAggregate : QLAggregate
    {
        public QLTupleAggregate(IReadOnlyList<QLAggregate> items) 
            : base(QLAggregateKind.Tuple)
        {
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<QLAggregate> Items { get; }
    }
}