//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlObjectProperty : SqlObject
    {
        private SqlObjectProperty(
             SqlPropertyName name,
             SqlScalarExpression value)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public SqlPropertyName Name { get; }

        public SqlScalarExpression Value { get; }

        public static SqlObjectProperty Create(
            SqlPropertyName name,
            SqlScalarExpression expression) => new SqlObjectProperty(name, expression);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
