//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlSelectStarSpec : SqlSelectSpec
    {
        public static readonly SqlSelectStarSpec Singleton = new SqlSelectStarSpec();

        private SqlSelectStarSpec()
            : base(SqlObjectKind.SelectStarSpec)
        {
        }

        public static SqlSelectStarSpec Create() => SqlSelectStarSpec.Singleton;

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlSelectSpecVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlSelectSpecVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlSelectSpecVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
