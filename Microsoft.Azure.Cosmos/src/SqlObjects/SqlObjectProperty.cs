//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlObjectProperty.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlObjectProperty : SqlObject
    {
        private SqlObjectProperty(
             SqlPropertyName name,
             SqlScalarExpression expression)
            : base(SqlObjectKind.ObjectProperty)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            this.Name = name;
            this.Expression = expression;
        }

        public SqlPropertyName Name
        {
            get;
        }

        public SqlScalarExpression Expression
        {
            get;
        }

        public static SqlObjectProperty Create(
            SqlPropertyName name,
            SqlScalarExpression expression)
        {
            return new SqlObjectProperty(name, expression);
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
    }
}
