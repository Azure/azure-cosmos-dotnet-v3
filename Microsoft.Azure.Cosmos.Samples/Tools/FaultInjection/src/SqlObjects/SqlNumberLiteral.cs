//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlNumberLiteral : SqlLiteral
    {
        private const int Capacity = 256;
        private static readonly Dictionary<long, SqlNumberLiteral> FrequentLongs = Enumerable
            .Range(-Capacity, Capacity)
            .ToDictionary(x => (long)x, x => new SqlNumberLiteral((long)x));
        private static readonly Dictionary<double, SqlNumberLiteral> FrequentDoubles = Enumerable
            .Range(-Capacity, Capacity)
            .ToDictionary(x => (double)x, x => new SqlNumberLiteral((double)x));

        private SqlNumberLiteral(Number64 value)
        {
            this.Value = value;
        }

        public Number64 Value { get; }

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

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlLiteralVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
