//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlLimitSpec : SqlObject
    {
        private const int PremadeLimitIndex = 256;
        private static readonly SqlLimitSpec[] PremadeLimitSpecs = Enumerable
            .Range(0, PremadeLimitIndex)
            .Select(limit => new SqlLimitSpec(
                SqlLiteralScalarExpression.Create(
                    SqlNumberLiteral.Create(limit))))
            .ToArray();

        private SqlLimitSpec(SqlScalarExpression limitExpression)
        {
            this.LimitExpression = limitExpression ?? throw new ArgumentNullException(nameof(limitExpression));
        }

        public SqlScalarExpression LimitExpression { get; }

        public static SqlLimitSpec Create(SqlNumberLiteral sqlNumberLiteral)
        {
            if (sqlNumberLiteral == null)
            {
                throw new ArgumentNullException(nameof(sqlNumberLiteral));
            }

            long value;
            if (!sqlNumberLiteral.Value.IsInteger)
            {
                throw new ArgumentOutOfRangeException($"Expected {nameof(sqlNumberLiteral)} to be an integer.");
            }

            value = Number64.ToLong(sqlNumberLiteral.Value);
            if (value < PremadeLimitIndex && value >= 0)
            {
                return SqlLimitSpec.PremadeLimitSpecs[value];
            }

            SqlScalarExpression limitExpression = SqlLiteralScalarExpression.Create(
                SqlNumberLiteral.Create(
                    value));
            return new SqlLimitSpec(limitExpression);
        }

        public static SqlLimitSpec Create(SqlParameter sqlParameter)
        {
            if (sqlParameter == null)
            {
                throw new ArgumentNullException(nameof(sqlParameter));
            }

            SqlParameterRefScalarExpression sqlParameterRefScalarExpression = SqlParameterRefScalarExpression.Create(sqlParameter);
            return new SqlLimitSpec(sqlParameterRefScalarExpression);
        }

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
