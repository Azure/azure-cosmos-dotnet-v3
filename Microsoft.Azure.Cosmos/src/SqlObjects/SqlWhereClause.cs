//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlWhereClause : SqlObject
    {
        public SqlScalarExpression FilterExpression
        {
            get;
            private set;
        }

        public SqlWhereClause(SqlScalarExpression filterExpression)
            : base(SqlObjectKind.WhereClause)
        {
            if (filterExpression == null)
            {
                throw new ArgumentNullException("filterExpression");
            }
            
            this.FilterExpression = filterExpression;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("WHERE ");
            this.FilterExpression.AppendToBuilder(builder);
        }
    }
}
