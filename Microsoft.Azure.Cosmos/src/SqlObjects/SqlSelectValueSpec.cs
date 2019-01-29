//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlSelectValueSpec.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlSelectValueSpec : SqlSelectSpec
    {
        private SqlSelectValueSpec(
            SqlScalarExpression expression)
            : base(SqlObjectKind.SelectValueSpec)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            this.Expression = expression;
        }

        public SqlScalarExpression Expression
        {
            get;
        }

        public static SqlSelectValueSpec Create(SqlScalarExpression expression)
        {
            return new SqlSelectValueSpec(expression);
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

        public override void Accept(SqlSelectSpecVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlSelectSpecVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlSelectSpecVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
