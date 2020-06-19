//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlLiteralScalarExpression : SqlScalarExpression
    {
        public static readonly SqlLiteralScalarExpression SqlNullLiteralScalarExpression = new SqlLiteralScalarExpression(SqlNullLiteral.Create());
        public static readonly SqlLiteralScalarExpression SqlTrueLiteralScalarExpression = new SqlLiteralScalarExpression(SqlBooleanLiteral.True);
        public static readonly SqlLiteralScalarExpression SqlFalseLiteralScalarExpression = new SqlLiteralScalarExpression(SqlBooleanLiteral.False);
        public static readonly SqlLiteralScalarExpression SqlUndefinedLiteralScalarExpression = new SqlLiteralScalarExpression(SqlUndefinedLiteral.Create());

        private SqlLiteralScalarExpression(SqlLiteral literal)
        {
            this.Literal = literal ?? throw new ArgumentNullException(nameof(literal));
        }

        public SqlLiteral Literal { get; }

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

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
