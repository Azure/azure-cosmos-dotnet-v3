//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlUserDefinedFunctionCallScalarExpression : CqlScalarExpression
    {
        public CqlUserDefinedFunctionCallScalarExpression(CqlFunctionIdentifier identifier, IReadOnlyList<CqlScalarExpression> arguments, bool builtin) 
            : base(CqlScalarExpressionKind.UserDefinedFunctionCall)
        {
            this.Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            this.Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            this.Builtin = builtin;
        }

        public CqlFunctionIdentifier Identifier { get; }

        public IReadOnlyList<CqlScalarExpression> Arguments { get; }
        
        public bool Builtin { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}