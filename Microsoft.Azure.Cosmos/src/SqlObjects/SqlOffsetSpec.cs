//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Linq;

    internal sealed class SqlOffsetSpec : SqlObject
    {
        private const int PremadeOffsetIndex = 256;
        private static readonly SqlOffsetSpec[] PremadeOffsetSpecs = Enumerable
            .Range(0, PremadeOffsetIndex)
            .Select(offset => new SqlOffsetSpec(
                SqlLiteralScalarExpression.Create(
                    SqlNumberLiteral.Create(offset))))
            .ToArray();

        private SqlOffsetSpec(SqlScalarExpression offsetExpression)
            : base(SqlObjectKind.OffsetSpec)
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
