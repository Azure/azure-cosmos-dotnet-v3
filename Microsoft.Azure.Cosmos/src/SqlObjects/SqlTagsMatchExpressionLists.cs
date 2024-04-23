//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    internal class SqlTagsMatchExpressionLists : SqlScalarExpression
    {
        private SqlTagsMatchExpressionLists(IEnumerable<SqlTagsMatchExpressionList> matches)
        {
            this.MatchesList = matches ?? new List<SqlTagsMatchExpressionList>();
        }

        public IEnumerable<SqlTagsMatchExpressionList> MatchesList { get; }

        public static SqlTagsMatchExpressionLists Create(IEnumerable<SqlTagsMatchExpressionList> matches)
            => new SqlTagsMatchExpressionLists(matches);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}