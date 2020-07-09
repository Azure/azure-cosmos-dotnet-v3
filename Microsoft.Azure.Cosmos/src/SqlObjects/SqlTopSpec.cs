//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlTopSpec : SqlObject
    {
        private const int PremadeTopIndex = 256;
        private static readonly ImmutableArray<SqlTopSpec> PremadeTopSpecs = Enumerable
            .Range(0, PremadeTopIndex)
            .Select(top => new SqlTopSpec(
                SqlLiteralScalarExpression.Create(
                    SqlNumberLiteral.Create(top))))
            .ToImmutableArray();

        private SqlTopSpec(SqlScalarExpression topExpression)
        {
            this.TopExpresion = topExpression ?? throw new ArgumentNullException(nameof(topExpression));
        }

        public SqlScalarExpression TopExpresion { get; }

        public static SqlTopSpec Create(SqlNumberLiteral sqlNumberLiteral)
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
            if ((value < PremadeTopIndex) && (value >= 0))
            {
                return SqlTopSpec.PremadeTopSpecs[(int)value];
            }

            SqlScalarExpression topExpression = SqlLiteralScalarExpression.Create(
                SqlNumberLiteral.Create(
                    value));
            return new SqlTopSpec(topExpression);
        }

        public static SqlTopSpec Create(SqlParameter sqlParameter)
        {
            if (sqlParameter == null)
            {
                throw new ArgumentNullException(nameof(sqlParameter));
            }

            SqlParameterRefScalarExpression sqlParameterRefScalarExpression = SqlParameterRefScalarExpression.Create(sqlParameter);
            return new SqlTopSpec(sqlParameterRefScalarExpression);
        }

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
