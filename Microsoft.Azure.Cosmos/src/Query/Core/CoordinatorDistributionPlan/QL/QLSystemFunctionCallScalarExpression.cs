//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLSystemFunctionCallScalarExpression : QLScalarExpression
    {
        public QLSystemFunctionCallScalarExpression(QLBuiltinScalarFunctionKind functionKind, IReadOnlyList<QLScalarExpression> arguments) 
            : base(QLScalarExpressionKind.SystemFunctionCall)
        {
            this.FunctionKind = functionKind;
            this.Arguments = arguments;
        }

        public QLBuiltinScalarFunctionKind FunctionKind { get; }
        
        public IReadOnlyList<QLScalarExpression> Arguments { get; }
    }

}