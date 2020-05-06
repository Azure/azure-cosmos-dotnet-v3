//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlJoinCollectionExpression : SqlCollectionExpression
    {
        private SqlJoinCollectionExpression(
            SqlCollectionExpression left,
            SqlCollectionExpression right)
        {
            this.Left = left;
            this.Right = right;
        }

        public SqlCollectionExpression Left { get; }

        public SqlCollectionExpression Right { get; }

        public static SqlJoinCollectionExpression Create(
            SqlCollectionExpression left,
            SqlCollectionExpression right) => new SqlJoinCollectionExpression(left, right);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlCollectionExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlCollectionExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlCollectionExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
