//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlArrayCreateScalarExpression : CqlScalarExpression
    {
        private const string Array = "Array";

        public CqlArrayCreateScalarExpression(IReadOnlyList<CqlScalarExpression> items) 
            : base(CqlScalarExpressionKind.ArrayCreate)
        {
            this.ArrayKind = Array;
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public string ArrayKind { get; }
        
        public IReadOnlyList<CqlScalarExpression> Items { get; }

        public override void Accept(ICqlVisitor cqlVisitor) => cqlVisitor.Visit(this);
    }
}