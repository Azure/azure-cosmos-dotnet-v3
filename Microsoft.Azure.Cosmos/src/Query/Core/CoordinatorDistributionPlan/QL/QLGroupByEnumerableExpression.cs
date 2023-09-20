//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLGroupByEnumerableExpression : QLEnumerableExpression
    {
        public QLGroupByEnumerableExpression(QLEnumerableExpression sourceExpression, ulong keyCount, IReadOnlyList<QLAggregate> aggregates) 
            : base(QLEnumerableExpressionKind.GroupBy)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.KeyCount = keyCount;
            this.Aggregates = aggregates ?? throw new ArgumentNullException(nameof(aggregates));
        }

        public QLEnumerableExpression SourceExpression { get; }

        public ulong KeyCount { get; }
        
        public IReadOnlyList<QLAggregate> Aggregates { get; }
    }
}