//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;
    using System.Collections.Generic;

    internal class CqlTupleCreateScalarExpression : CqlScalarExpression
    {
        public CqlTupleCreateScalarExpression(IReadOnlyList<CqlScalarExpression> items) 
            : base(CqlScalarExpressionKind.TupleCreate)
        {
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<CqlScalarExpression> Items { get; }
    }
}