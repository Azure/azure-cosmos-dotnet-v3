//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBinaryScalarExpression : ClientQLScalarExpression
    {
        public ClientQLBinaryScalarExpression(ClientQLBinaryScalarOperatorKind operatorKind, int maxDepth, ClientQLScalarExpression leftExpression, ClientQLScalarExpression rightExpression) 
            : base(ClientQLScalarExpressionKind.BinaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.MaxDepth = maxDepth;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public ClientQLBinaryScalarOperatorKind OperatorKind { get; }

        public int MaxDepth { get; }

        public ClientQLScalarExpression LeftExpression { get; }
        
        public ClientQLScalarExpression RightExpression { get; }
    }

}