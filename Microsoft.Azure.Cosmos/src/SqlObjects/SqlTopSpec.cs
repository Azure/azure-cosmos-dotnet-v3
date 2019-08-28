//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Linq;

    internal sealed class SqlTopSpec : SqlObject
    {
        private const int PremadeTopIndex = 256;
        private static readonly SqlTopSpec[] PremadeTopSpecs = Enumerable.Range(0, PremadeTopIndex).Select(top => new SqlTopSpec(top)).ToArray();

        private SqlTopSpec(long count)
            : base(SqlObjectKind.TopSpec)
        {
            this.Count = count;
        }

        public long Count
        {
            get;
        }

        public static SqlTopSpec Create(long value)
        {
            if (value < PremadeTopIndex && value >= 0)
            {
                return SqlTopSpec.PremadeTopSpecs[value];
            }

            return new SqlTopSpec(value);
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
