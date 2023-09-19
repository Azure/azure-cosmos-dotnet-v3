//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLMuxScalarExpression : QLScalarExpression
    {
        public QLMuxScalarExpression(QLScalarExpression conditionExpression, QLScalarExpression leftExpression, QLScalarExpression rightExpression) 
            : base(QLScalarExpressionKind.Mux)
        {
            this.ConditionExpression = conditionExpression;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public QLScalarExpression ConditionExpression { get; }

        public QLScalarExpression LeftExpression { get; }
        
        public QLScalarExpression RightExpression { get; }
    }
}