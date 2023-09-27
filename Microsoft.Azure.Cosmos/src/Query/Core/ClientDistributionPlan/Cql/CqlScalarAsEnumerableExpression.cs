//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlScalarAsEnumerableExpression : CqlEnumerableExpression
    {
        public CqlScalarAsEnumerableExpression(CqlScalarExpression expression, CqlEnumerationKind enumerationKind) 
            : base(CqlEnumerableExpressionKind.ScalarAsEnumerable)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.EnumerationKind = enumerationKind;
        }

        public CqlScalarExpression Expression { get; }

        public CqlEnumerationKind EnumerationKind { get; }
    }
}