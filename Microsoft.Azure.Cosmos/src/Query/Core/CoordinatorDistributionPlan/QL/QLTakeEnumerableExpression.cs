//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System;

    internal class QLTakeEnumerableExpression : QLEnumerableExpression
    {
        public QLTakeEnumerableExpression(QLEnumerableExpression sourceExpression, ulong skipValue, ulong takeValue)
            : base(QLEnumerableExpressionKind.Take)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.SkipValue = skipValue;
            this.TakeValue = takeValue;
        }

        public QLEnumerableExpression SourceExpression { get; }

        public ulong SkipValue { get; }
        
        public ulong TakeValue { get; }
    }
}