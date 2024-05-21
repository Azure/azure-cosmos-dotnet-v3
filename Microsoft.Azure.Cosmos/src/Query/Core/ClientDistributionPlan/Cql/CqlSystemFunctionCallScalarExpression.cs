//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlSystemFunctionCallScalarExpression : CqlScalarExpression
    {
        public CqlSystemFunctionCallScalarExpression(CqlBuiltinScalarFunctionKind functionKind, IReadOnlyList<CqlScalarExpression> arguments) 
            : base(CqlScalarExpressionKind.SystemFunctionCall)
        {
            this.FunctionKind = functionKind;
            this.Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        public CqlBuiltinScalarFunctionKind FunctionKind { get; }
        
        public IReadOnlyList<CqlScalarExpression> Arguments { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}