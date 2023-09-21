//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLMuxScalarExpression : QLScalarExpression
    {
        public QLMuxScalarExpression(QLScalarExpression conditionExpression, QLScalarExpression leftExpression, QLScalarExpression rightExpression) 
            : base(QLScalarExpressionKind.Mux)
        {
            this.ConditionExpression = conditionExpression ?? throw new ArgumentNullException(nameof(conditionExpression));
            this.LeftExpression = leftExpression ?? throw new ArgumentNullException(nameof(leftExpression));
            this.RightExpression = rightExpression ?? throw new ArgumentNullException(nameof(rightExpression));
        }

        public QLScalarExpression ConditionExpression { get; }

        public QLScalarExpression LeftExpression { get; }
        
        public QLScalarExpression RightExpression { get; }
    }
}