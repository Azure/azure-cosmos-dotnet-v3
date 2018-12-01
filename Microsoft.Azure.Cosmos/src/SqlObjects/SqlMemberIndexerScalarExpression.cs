//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
using System;
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlMemberIndexerScalarExpression : SqlScalarExpression
    {
        public SqlScalarExpression MemberExpression
        {
            get;
            private set;
        }

        public SqlScalarExpression IndexExpression
        {
            get;
            private set;
        }

        public SqlMemberIndexerScalarExpression(
            SqlScalarExpression memberExpression,
            SqlScalarExpression indexExpression)
            : base(SqlObjectKind.MemberIndexerScalarExpression)
        {
            if(memberExpression == null)
            {
                throw new ArgumentNullException("memberExpression");
            }

            if(indexExpression == null)
            {
                throw new ArgumentNullException("indexExpression");
            }

            this.MemberExpression = memberExpression;
            this.IndexExpression = indexExpression;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            this.MemberExpression.AppendToBuilder(builder);
            builder.Append("[");
            this.IndexExpression.AppendToBuilder(builder);
            builder.Append("]");
        }
    }
}
