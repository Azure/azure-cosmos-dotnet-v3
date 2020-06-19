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
    sealed class SqlOffsetSpec : SqlObject
    {
        private const int PremadeOffsetIndex = 256;
        private static readonly SqlOffsetSpec[] PremadeOffsetSpecs = Enumerable
            .Range(0, PremadeOffsetIndex)
            .Select(offset => new SqlOffsetSpec(
                SqlLiteralScalarExpression.Create(
                    SqlNumberLiteral.Create(offset))))
            .ToArray();

        private SqlOffsetSpec(SqlScalarExpression offsetExpression)
        {
            this.OffsetExpression = offsetExpression ?? throw new ArgumentNullException(nameof(offsetExpression));
        }

        public SqlScalarExpression OffsetExpression { get; }

        public static SqlOffsetSpec Create(SqlNumberLiteral sqlNumberLiteral)
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
            if (value < PremadeOffsetIndex && value >= 0)
            {
                return SqlOffsetSpec.PremadeOffsetSpecs[value];
            }

            SqlScalarExpression offsetExpression = SqlLiteralScalarExpression.Create(
                SqlNumberLiteral.Create(
                    value));
            return new SqlOffsetSpec(offsetExpression);
        }

        public static SqlOffsetSpec Create(SqlParameter sqlParameter)
        {
            if (sqlParameter == null)
            {
                throw new ArgumentNullException(nameof(sqlParameter));
            }

            SqlParameterRefScalarExpression sqlParameterRefScalarExpression = SqlParameterRefScalarExpression.Create(sqlParameter);
            return new SqlOffsetSpec(sqlParameterRefScalarExpression);
        }

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
