//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    using System.Collections.Generic;

    internal class QLUserDefinedFunctionCallScalarExpression : QLScalarExpression
    {
        public QLUserDefinedFunctionCallScalarExpression(QLFunctionIdentifier identifier, IReadOnlyList<QLScalarExpression> arguments, bool builtin) 
            : base(QLScalarExpressionKind.UserDefinedFunctionCall)
        {
            this.Identifier = identifier;
            this.Arguments = arguments;
            this.Builtin = builtin;
        }

        public QLFunctionIdentifier Identifier { get; }

        public IReadOnlyList<QLScalarExpression> Arguments { get; }
        
        public bool Builtin { get; }
    }
}