//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlTupleAggregate : CqlAggregate
    {
        public CqlTupleAggregate(IReadOnlyList<CqlAggregate> items) 
            : base(CqlAggregateKind.Tuple)
        {
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<CqlAggregate> Items { get; }
    }
}