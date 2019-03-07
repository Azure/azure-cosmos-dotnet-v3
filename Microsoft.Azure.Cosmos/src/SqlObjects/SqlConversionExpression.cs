//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlConversionScalarExpression.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal sealed class SqlConversionScalarExpression : SqlScalarExpression
    {
        public readonly SqlScalarExpression expression;
        private readonly Type sourceType;
        private readonly Type targetType;

        private SqlConversionScalarExpression(SqlScalarExpression expression, Type sourceType, Type targetType)
            : base(SqlObjectKind.ConversionScalarExpression)
        {
            this.expression = expression;
            this.sourceType = sourceType;
            this.targetType = targetType;
        }

        public static SqlConversionScalarExpression Create(SqlScalarExpression expression, Type sourceType, Type targetType)
        {
            return new SqlConversionScalarExpression(expression, sourceType, targetType);
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
