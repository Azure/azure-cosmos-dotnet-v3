//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlMemberIndexerScalarExpression : SqlScalarExpression
    {
        private SqlMemberIndexerScalarExpression(
            SqlScalarExpression member,
            SqlScalarExpression indexer)
            : base(SqlObjectKind.MemberIndexerScalarExpression)
        {
            this.Member = member ?? throw new ArgumentNullException("memberExpression");
            this.Indexer = indexer ?? throw new ArgumentNullException("indexExpression");
        }

        public SqlScalarExpression Member
        {
            get;
        }

        public SqlScalarExpression Indexer
        {
            get;
        }

        public static SqlMemberIndexerScalarExpression Create(
            SqlScalarExpression member,
            SqlScalarExpression indexer)
        {
            return new SqlMemberIndexerScalarExpression(member, indexer);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
