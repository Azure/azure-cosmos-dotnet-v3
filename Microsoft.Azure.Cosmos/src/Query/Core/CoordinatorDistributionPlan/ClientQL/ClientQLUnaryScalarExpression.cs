//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLUnaryScalarExpression : ClientQLScalarExpression
    {
        public ClientQLUnaryScalarExpression(ClientQLUnaryScalarOperatorKind operatorKind, ClientQLScalarExpression expression) 
            : base(ClientQLScalarExpressionKind.UnaryOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression;
        }

        public ClientQLUnaryScalarOperatorKind OperatorKind { get; }
        
        public ClientQLScalarExpression Expression { get; }
    }

}