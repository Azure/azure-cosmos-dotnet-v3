//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLTupleItemRefScalarExpression : ClientQLScalarExpression
    {
        public ClientQLTupleItemRefScalarExpression(ClientQLScalarExpression expression, int index) 
            : base(ClientQLScalarExpressionKind.TupleItemRef)
        {
            this.Expression = expression;
            this.Index = index;
        }

        public ClientQLScalarExpression Expression { get; }
        
        public int Index { get; }
    }

}