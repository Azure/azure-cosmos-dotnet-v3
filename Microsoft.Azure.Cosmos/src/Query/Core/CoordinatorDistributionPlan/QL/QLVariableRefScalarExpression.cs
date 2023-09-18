//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLVariableRefScalarExpression : QLScalarExpression
    {
        public QLVariableRefScalarExpression(QLVariable variable)
            : base(QLScalarExpressionKind.VariableRef)
        {
            this.Variable = variable;
        }

        public QLVariable Variable { get; }
    }

}