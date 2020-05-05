//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;

    internal sealed class SqlTopSpec : SqlObject
    {
        private const int PremadeTopIndex = 256;
        private static readonly ImmutableArray<SqlTopSpec> PremadeTopSpecs = Enumerable
            .Range(0, PremadeTopIndex)
            .Select(top => new SqlTopSpec(
                SqlLiteralScalarExpression.Create(
                    SqlNumberLiteral.Create(top))))
            .ToImmutableArray();

        private SqlTopSpec(SqlScalarExpression topExpression)
            : base(SqlObjectKind.TopSpec)
        {
            if (topExpression == null)
            {
                throw new ArgumentNullException(nameof(topExpression));
            }

            this.TopExpresion = topExpression;
        }

        public SqlScalarExpression TopExpresion
        {
            get;
        }

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
