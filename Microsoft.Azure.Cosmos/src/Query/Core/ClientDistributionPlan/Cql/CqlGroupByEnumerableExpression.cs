//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlGroupByEnumerableExpression : CqlEnumerableExpression
    {
        public CqlGroupByEnumerableExpression(CqlEnumerableExpression sourceExpression, ulong keyCount, IReadOnlyList<CqlAggregate> aggregates) 
            : base(CqlEnumerableExpressionKind.GroupBy)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.KeyCount = keyCount;
            this.Aggregates = aggregates ?? throw new ArgumentNullException(nameof(aggregates));
        }

        public CqlEnumerableExpression SourceExpression { get; }

        public ulong KeyCount { get; }
        
        public IReadOnlyList<CqlAggregate> Aggregates { get; }
    }
}