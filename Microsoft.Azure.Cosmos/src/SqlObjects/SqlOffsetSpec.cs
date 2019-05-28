//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Linq;

    internal sealed class SqlOffsetSpec : SqlObject
    {
        private const int PremadeOffsetIndex = 256;
        private static readonly SqlOffsetSpec[] PremadeOffsetSpecs = Enumerable.Range(0, PremadeOffsetIndex).Select(offset => new SqlOffsetSpec(offset)).ToArray();

        private SqlOffsetSpec(long offset)
            : base(SqlObjectKind.OffsetSpec)
        {
            this.Offset = offset;
        }

        public long Offset
        {
            get;
        }

        public static SqlOffsetSpec Create(long value)
        {
            if (value < PremadeOffsetIndex && value >= 0)
            {
                return SqlOffsetSpec.PremadeOffsetSpecs[value];
            }

            return new SqlOffsetSpec(value);
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
