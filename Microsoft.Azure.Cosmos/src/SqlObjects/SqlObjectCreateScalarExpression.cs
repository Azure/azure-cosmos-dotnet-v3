//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal sealed class SqlObjectCreateScalarExpression : SqlScalarExpression
    {
        private SqlObjectCreateScalarExpression(ImmutableArray<SqlObjectProperty> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException($"{nameof(properties)} must not be null.");
            }

            foreach (SqlObjectProperty property in properties)
            {
                if (property == null)
                {
                    throw new ArgumentException($"{nameof(properties)} must not have null items.");
                }
            }

            this.Properties = properties;
        }

        public ImmutableArray<SqlObjectProperty> Properties { get; }

        public static SqlObjectCreateScalarExpression Create(params SqlObjectProperty[] properties) => new SqlObjectCreateScalarExpression(properties.ToImmutableArray());

        public static SqlObjectCreateScalarExpression Create(ImmutableArray<SqlObjectProperty> properties) => new SqlObjectCreateScalarExpression(properties);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
