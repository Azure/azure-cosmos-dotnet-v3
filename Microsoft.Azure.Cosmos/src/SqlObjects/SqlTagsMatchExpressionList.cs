//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    internal class SqlTagsMatchExpressionList : SqlScalarExpression
    {
        private SqlTagsMatchExpressionList(IEnumerable<SqlTagsMatchExpression> matches)
        {
            this.MatchesList = matches ?? new List<SqlTagsMatchExpression>();
        }

        public IEnumerable<SqlTagsMatchExpression> MatchesList { get; }

        public static SqlTagsMatchExpressionList Create(IEnumerable<SqlTagsMatchExpression> matches)
            => new SqlTagsMatchExpressionList(matches);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}