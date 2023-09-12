//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLBinaryScalarExpression : ClientQLScalarExpression
    {
        public ClientQLBinaryScalarExpression(ClientQLBinaryScalarOperatorKind operatorKind, ClientQLScalarExpression leftExpression, ClientQLScalarExpression rightExpression) 
            : base(ClientQLScalarExpressionKind.BinaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public ClientQLBinaryScalarOperatorKind OperatorKind { get; }

        public ClientQLScalarExpression LeftExpression { get; }
        
        public ClientQLScalarExpression RightExpression { get; }
    }
}