//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;

    internal class QLVariableRefScalarExpression : QLScalarExpression
    {
        public QLVariableRefScalarExpression(QLVariable variable)
            : base(QLScalarExpressionKind.VariableRef)
        {
            this.Variable = variable ?? throw new ArgumentNullException(nameof(variable));
        }

        public QLVariable Variable { get; }
    }
}