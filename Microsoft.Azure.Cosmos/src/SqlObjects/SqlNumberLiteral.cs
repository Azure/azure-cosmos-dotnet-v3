//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class SqlNumberLiteral : SqlLiteral
    {
        private const int Capacity = 256;
        private static readonly Dictionary<long, SqlNumberLiteral> FrequentLongs = Enumerable
            .Range(-Capacity, Capacity)
            .ToDictionary(x => (long)x, x => new SqlNumberLiteral((long)x));
        private static readonly Dictionary<double, SqlNumberLiteral> FrequentDoubles = Enumerable
            .Range(-Capacity, Capacity)
            .ToDictionary(x => (double)x, x => new SqlNumberLiteral((double)x));

        private SqlNumberLiteral(Number64 value)
            : base(SqlObjectKind.NumberLiteral)
        {
            this.Value = value;
        }

        public Number64 Value
        {
            get;
        }

        public static SqlNumberLiteral Create(decimal number)
        {
            SqlNumberLiteral sqlNumberLiteral;
            if ((number >= long.MinValue) && (number <= long.MaxValue) && (number % 1 == 0))
            {
                sqlNumberLiteral = Create(Convert.ToInt64(number));
            }
            else
            {
                sqlNumberLiteral = Create(Convert.ToDouble(number));
            }

            return sqlNumberLiteral;
        }

        public static SqlNumberLiteral Create(double number)
        {
            SqlNumberLiteral sqlNumberLiteral;
            if (!SqlNumberLiteral.FrequentDoubles.TryGetValue(number, out sqlNumberLiteral))
            {
                sqlNumberLiteral = new SqlNumberLiteral(number);
            }

            return sqlNumberLiteral;
        }

        public static SqlNumberLiteral Create(long number)
        {
            SqlNumberLiteral sqlNumberLiteral;
            if (!SqlNumberLiteral.FrequentLongs.TryGetValue(number, out sqlNumberLiteral))
            {
                sqlNumberLiteral = new SqlNumberLiteral(number);
            }

            return sqlNumberLiteral;
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

        public override void Accept(SqlLiteralVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
