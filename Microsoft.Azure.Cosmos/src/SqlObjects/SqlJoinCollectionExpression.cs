//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlJoinCollectionExpression : SqlCollectionExpression
    {
        public SqlCollectionExpression LeftExpression
        {
            get;
            private set;
        }

        public SqlCollectionExpression RightExpression
        {
            get;
            private set;
        }

        public SqlJoinCollectionExpression(
            SqlCollectionExpression leftExpression,
            SqlCollectionExpression rightExpression)
            : base(SqlObjectKind.JoinCollectionExpression)
        {
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            this.LeftExpression.AppendToBuilder(builder);
            builder.Append(" JOIN ");
            this.RightExpression.AppendToBuilder(builder);
        }
    }
}
