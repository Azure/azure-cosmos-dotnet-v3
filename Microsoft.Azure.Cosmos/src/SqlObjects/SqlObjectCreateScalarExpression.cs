//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlObjectCreateScalarExpression : SqlScalarExpression
    {
        private SqlObjectCreateScalarExpression(IEnumerable<SqlObjectProperty> properties)
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

            this.Properties = new List<SqlObjectProperty>(properties);
        }

        public IEnumerable<SqlObjectProperty> Properties { get; }

        public static SqlObjectCreateScalarExpression Create(params SqlObjectProperty[] properties) => new SqlObjectCreateScalarExpression(properties);

        public static SqlObjectCreateScalarExpression Create(IEnumerable<SqlObjectProperty> properties) => new SqlObjectCreateScalarExpression(properties);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
