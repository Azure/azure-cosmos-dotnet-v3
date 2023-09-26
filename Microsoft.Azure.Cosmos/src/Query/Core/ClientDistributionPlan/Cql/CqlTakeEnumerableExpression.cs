//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlTakeEnumerableExpression : CqlEnumerableExpression
    {
        public CqlTakeEnumerableExpression(CqlEnumerableExpression sourceExpression, ulong skipValue, ulong takeValue)
            : base(CqlEnumerableExpressionKind.Take)
        {
            this.SourceExpression = sourceExpression ?? throw new ArgumentNullException(nameof(sourceExpression));
            this.SkipValue = skipValue;
            this.TakeValue = takeValue;
        }

        public CqlEnumerableExpression SourceExpression { get; }

        public ulong SkipValue { get; }
        
        public ulong TakeValue { get; }
    }
}