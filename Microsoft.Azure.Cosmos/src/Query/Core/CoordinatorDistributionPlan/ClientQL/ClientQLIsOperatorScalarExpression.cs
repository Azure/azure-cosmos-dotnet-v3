//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLIsOperatorScalarExpression : ClientQLScalarExpression
    {
        public ClientQLIsOperatorScalarExpression(ClientQLIsOperatorKind operatorKind, ClientQLScalarExpression expression) 
            : base(ClientQLScalarExpressionKind.IsOperator)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression;
        }

        public ClientQLIsOperatorKind OperatorKind { get; }
        
        public ClientQLScalarExpression Expression { get; }
    }

}