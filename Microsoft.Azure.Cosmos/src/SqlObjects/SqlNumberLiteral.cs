//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Collections.Immutable;
    using System.Linq;

    internal sealed class SqlNumberLiteral : SqlLiteral
    {
        private const int Capacity = 256;
        private static readonly ImmutableDictionary<long, SqlNumberLiteral> FrequentLongs = Enumerable
            .Range(-Capacity, Capacity)
            .ToDictionary(x => (long)x, x => new SqlNumberLiteral((long)x))
            .ToImmutableDictionary();
        private static readonly ImmutableDictionary<double, SqlNumberLiteral> FrequentDoubles = Enumerable
            .Range(-Capacity, Capacity)
            .ToDictionary(x => (double)x, x => new SqlNumberLiteral((double)x))
            .ToImmutableDictionary();

        private SqlNumberLiteral(Number64 value)
            : base(SqlObjectKind.NumberLiteral)
        {
            this.Value = value;
        }

        public Number64 Value
        {
            get;
        }

        public static SqlNumberLiteral Create(Number64 number64)
        {
            SqlNumberLiteral sqlNumberLiteral;
            if (number64.IsDouble)
            {
                if (!SqlNumberLiteral.FrequentDoubles.TryGetValue(Number64.ToDouble(number64), out sqlNumberLiteral))
                {
                    sqlNumberLiteral = new SqlNumberLiteral(number64);
                }
            }
            else
            {
                if (!SqlNumberLiteral.FrequentLongs.TryGetValue(Number64.ToLong(number64), out sqlNumberLiteral))
                {
                    sqlNumberLiteral = new SqlNumberLiteral(number64);
                }
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
