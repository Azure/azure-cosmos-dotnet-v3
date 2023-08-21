//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLVariableRefScalarExpression : ClientQLScalarExpression
    {
        public ClientQLVariableRefScalarExpression(ClientQLVariable variable)
            : base(ClientQLScalarExpressionKind.VariableRef)
        {
            this.Variable = variable;
        }

        public ClientQLVariable Variable { get; }
    }

}