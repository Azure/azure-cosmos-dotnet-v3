//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLTakeEnumerableExpression : QLEnumerableExpression
    {
        public QLTakeEnumerableExpression(QLEnumerableExpression sourceExpression, long skipValue, long takeValue)
            : base(QLEnumerableExpressionKind.Take)
        {
            this.SourceExpression = sourceExpression;
            this.SkipValue = skipValue;
            this.TakeValue = takeValue;
        }

        public QLEnumerableExpression SourceExpression { get; }

        public long SkipValue { get; }
        
        public long TakeValue { get; }
    }

}