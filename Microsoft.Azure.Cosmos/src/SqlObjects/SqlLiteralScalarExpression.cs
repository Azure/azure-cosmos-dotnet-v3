//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlLiteralScalarExpression : SqlScalarExpression
    {
        public static readonly SqlLiteralScalarExpression SqlNullLiteralScalarExpression = new SqlLiteralScalarExpression(SqlNullLiteral.Create());
        public static readonly SqlLiteralScalarExpression SqlTrueLiteralScalarExpression = new SqlLiteralScalarExpression(SqlBooleanLiteral.True);
        public static readonly SqlLiteralScalarExpression SqlFalseLiteralScalarExpression = new SqlLiteralScalarExpression(SqlBooleanLiteral.False);
        public static readonly SqlLiteralScalarExpression SqlUndefinedLiteralScalarExpression = new SqlLiteralScalarExpression(SqlUndefinedLiteral.Create());

        private SqlLiteralScalarExpression(SqlLiteral literal)
            : base(SqlObjectKind.LiteralScalarExpression)
        {
            this.Literal = literal ?? throw new ArgumentNullException("literal");
        }

        public SqlLiteral Literal
        {
            get;
        }

        public static SqlLiteralScalarExpression Create(SqlLiteral sqlLiteral)
        {
            SqlLiteralScalarExpression sqlLiteralScalarExpression;
            if (sqlLiteral == SqlBooleanLiteral.True)
            {
                sqlLiteralScalarExpression = SqlLiteralScalarExpression.SqlTrueLiteralScalarExpression;
            }
            else if (sqlLiteral == SqlBooleanLiteral.False)
            {
                sqlLiteralScalarExpression = SqlLiteralScalarExpression.SqlFalseLiteralScalarExpression;
            }
            else if (sqlLiteral == SqlNullLiteral.Singleton)
            {
                sqlLiteralScalarExpression = SqlLiteralScalarExpression.SqlNullLiteralScalarExpression;
            }
            else if (sqlLiteral == SqlUndefinedLiteral.Create())
            {
                sqlLiteralScalarExpression = SqlLiteralScalarExpression.SqlUndefinedLiteralScalarExpression;
            }
            else
            {
                // Either a number or string in which we need to new one up.
                sqlLiteralScalarExpression = new SqlLiteralScalarExpression(sqlLiteral);
            }

            return sqlLiteralScalarExpression;
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
