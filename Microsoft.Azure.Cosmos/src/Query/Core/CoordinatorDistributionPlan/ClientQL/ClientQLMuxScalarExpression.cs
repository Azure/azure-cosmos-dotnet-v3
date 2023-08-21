//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMuxScalarExpression : ClientQLScalarExpression
    {
        public ClientQLMuxScalarExpression(ClientQLScalarExpression conditionExpression, ClientQLScalarExpression leftExpression, ClientQLScalarExpression rightExpression) 
            : base(ClientQLScalarExpressionKind.Mux)
        {
            this.ConditionExpression = conditionExpression;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public ClientQLScalarExpression ConditionExpression { get; }

        public ClientQLScalarExpression LeftExpression { get; }
        
        public ClientQLScalarExpression RightExpression { get; }
    }

}